using Godot;

namespace HackerOrSoldier;

// One class drives every enemy type; the behavior differences come from
// EnemyDef. Enemies collide with tiles only (the agent walks through them,
// damage is contact-based with a per-enemy cooldown).
public partial class Enemy : CharacterBody2D
{
    private const float SpeedUnit = 40f;         // px/s per speed point in the README table
    private const float EnemyGravity = 1200f;
    private const float RowSightRange = 320f;    // 10 tiles
    private const float AllSightRange = 256f;    // 8 tiles
    private const float ContactCooldownTime = 1f;

    public EnemyDef Def { get; set; }
    public PlaySession Session { get; set; }

    private int _lives;
    private int _facing = -1;
    private float _shootCooldown = 0.5f;   // small delay before the first attack
    private float _contactCooldown;
    private Sprite2D _sprite;
    private EnemyAnimator _animator;

    public bool IsTurnedOff => Def.Type == EnemyDefs.Robot && Session.RobotsOff;
    public string AnimationFrame => _animator?.Frame;
    public Rect2 Hitbox => new(Position + Def.Body.Position, Def.Body.Size);

    public override void _Ready()
    {
        _lives = StageLives(Def);
        CollisionLayer = 0;
        CollisionMask = 1;
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = Def.Body.Size },
            Position = Def.Body.GetCenter(),
        });
        _sprite = new Sprite2D { Texture = Sprites.ForObject(Def.Type) };
        AddChild(_sprite);
        _animator = new EnemyAnimator(_sprite, Def.Type);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _shootCooldown -= dt;
        _contactCooldown -= dt;

        if (IsTurnedOff)
        {
            // README: the robot stops and turns off every 10 seconds; only
            // then can it be killed.
            _sprite.Modulate = new Color(0.4f, 0.4f, 0.5f);
            Velocity = Vector2.Zero;
            _animator.Update(dt, Vector2.Zero, turnedOff: true, _facing);
            return;
        }
        _sprite.Modulate = Colors.White;

        bool spotted = SeesAgent();
        bool ranged = Def.Attack is EnemyAttack.Pistol or EnemyAttack.MiniGun or EnemyAttack.Plasma;
        float speed = Def.Speed * SpeedUnit * (Session.AlarmActive ? 2f : 1f);   // README: alarm doubles enemy speed

        if (spotted && Def.Attack == EnemyAttack.Contact)
            _facing = Session.Agent.Position.X < Position.X ? -1 : 1;   // chase
        else if (spotted && Def.Speed == 0 && Def.LoS == EnemyLoS.All)
            _facing = Session.Agent.Position.X < Position.X ? -1 : 1;   // the turret tracks its target

        Vector2 velocity = Velocity;
        bool stands = Def.Speed == 0 || (ranged && spotted);
        velocity.X = stands ? 0f : _facing * speed;
        velocity.Y = Def.Flies || Def.Swims || Def.Speed == 0 ? 0f : velocity.Y + EnemyGravity * dt;
        Velocity = velocity;

        if (Def.Speed > 0)
        {
            MoveAndSlide();
            if (IsOnWall())
                _facing = -_facing;
            if (Def.Swims)
            {
                if (Session.TileAt(
                        (int)Mathf.Floor((FrontX + _facing * 3) / 32f),
                        (int)Mathf.Floor(Position.Y / 32f)) != TileDefs.Water)
                    _facing = -_facing;   // eels stay inside the water
            }
            else if (Def.Flies)
            {
                if (Position.X < 16)
                    _facing = 1;
                else if (Position.X > Session.LevelRight - 16)
                    _facing = -1;
            }
            else if (IsOnFloor() && !SolidAt(
                    (int)Mathf.Floor((FrontX + _facing * 2) / 32f),
                    (int)Mathf.Floor((Position.Y + Def.Body.End.Y + 2) / 32f)))
            {
                _facing = -_facing;   // do not walk off ledges
            }
        }

        if (spotted && _shootCooldown <= 0)
            Attack();

        _animator.Update(dt, Velocity, turnedOff: false, _facing);
        CheckContact();
    }

    public void TakeHit()
    {
        if (Def.Type == EnemyDefs.Robot)
        {
            if (!Session.RobotsOff)
            {
                Sfx.PlayAt(Sound.BulletHit, Position);   // the shot pings off the live robot
                return;
            }
            Die();
            return;
        }
        if (Def.InfiniteLives)
            return;   // landmines cannot be shot
        if (Def.DegradesTo != null)
        {
            // README low difficulty: the super soldier does not change to a
            // soldier, it dies with a tombstone.
            if (Profile.Current.IsLowDifficulty && Def.Type == EnemyDefs.SuperSoldier)
            {
                Die();
                return;
            }
            // README: Ultimate -> Super -> Soldier, one stage per hit.
            Def = EnemyDefs.Get(Def.DegradesTo);
            _lives = StageLives(Def);
            _animator.SetType(Def.Type);
            Sfx.PlayAt(Sound.EnemyHit, Position);
            return;
        }
        _lives--;
        if (_lives <= 0)
            Die();
        else
            Sfx.PlayAt(Sound.EnemyHit, Position);
    }

    // README difficulty: enemies get -1/+1 life on low/hard, minimum 1.
    // Degrading stages always take one hit; the delta lands on the final stage.
    private static int StageLives(EnemyDef def) =>
        def.DegradesTo != null ? 1 : Mathf.Max(1, def.Lives + Profile.Current.EnemyLivesDelta);

    private void Die()
    {
        Sfx.PlayAt(Sound.EnemyDie, Position);
        if (Def.DropsAmmo && GD.Randf() < Session.DropChance)
            Session.SpawnAmmoPack(Position);
        if (Def.Type is EnemyDefs.Soldier or EnemyDefs.SuperSoldier)
            Session.SpawnTombstone(Position);   // README: the soldier chain ends with a RIP tombstone
        Session.RegisterKill();
        Session.OnEnemyDied(this);
        QueueFree();
    }

    private bool SeesAgent()
    {
        if (Session.Agent.IsHiding || Session.DisguiseActive)
            return false;   // README: closets hide the agent; the briefcase disguise fools enemies
        var to = Session.Agent.Position - Position;
        switch (Def.LoS)
        {
            case EnemyLoS.Row:
                if (Mathf.Abs(to.Y) > 24 || Mathf.Abs(to.X) > RowSightRange)
                    return false;
                if (Mathf.Sign(to.X) != _facing)
                    return false;
                return LineClear(Session.Agent.Position);
            case EnemyLoS.All:
                return to.Length() <= AllSightRange && LineClear(Session.Agent.Position);
            default:
                return false;
        }
    }

    private bool LineClear(Vector2 target) => Session.LineClear(Position, target);

    private void Attack()
    {
        if (Def.Attack != EnemyAttack.Contact && Def.Attack != EnemyAttack.Mine)
            _animator.Attack();
        switch (Def.Attack)
        {
            case EnemyAttack.Pistol:
                _shootCooldown = 1.5f;
                Sfx.PlayAt(Sound.EnemyShot, Position);
                Session.SpawnEnemyBullet(Position, new Vector2(_facing, 0), "bullet");
                break;
            case EnemyAttack.MiniGun:
                _shootCooldown = 0.6f;
                Sfx.PlayAt(Sound.EnemyMinigun, Position);
                Session.SpawnEnemyBullet(Position, new Vector2(_facing, 0), "bullet");
                break;
            case EnemyAttack.Plasma:
                _shootCooldown = 2f;
                Sfx.PlayAt(Sound.EnemyPlasma, Position);
                Session.SpawnEnemyBullet(Position,
                    (Session.Agent.Position - Position).Normalized(), "plasma");
                break;
            case EnemyAttack.Dynamite:
                _shootCooldown = 4f;
                Session.SpawnDynamite(Position);
                break;
        }
    }

    private void CheckContact()
    {
        if (Session.Agent.IsHiding)
            return;
        // Shrunk by a pixel so standing flush beside the agent is not contact.
        if (!Session.Agent.Hitbox.Intersects(Hitbox.Grow(-1)))
            return;
        if (Def.Attack == EnemyAttack.Mine)
        {
            // README: landmines trigger on impact.
            Session.OnEnemyDied(this);
            QueueFree();
            Session.Explode(Position);
            return;
        }
        if (_contactCooldown > 0)
            return;
        _contactCooldown = ContactCooldownTime;
        Session.AgentHit();
    }

    private float FrontX => Position.X + (_facing > 0 ? Def.Body.End.X : Def.Body.Position.X);

    private bool SolidAt(int x, int y) => TileDefs.Get(Session.TileAt(x, y))?.Solid == true;
}
