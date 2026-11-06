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

    private const float LurkTime = 1.5f;
    private const int LeapTiles = 3;

    private enum LeapPhase { Lurk, Leap }

    public EnemyDef Def { get; set; }
    public PlaySession Session { get; set; }

    private int _lives;
    private int _facing = -1;
    private float _shootCooldown = 0.5f;   // small delay before the first attack
    private float _contactCooldown;
    private Sprite2D _sprite;
    private EnemyAnimator _animator;   // every enemy but the turret
    private TurretGun _gun;            // the turret only
    private LeapPhase _leapPhase;      // leapers only
    private Vector2 _home;
    private float _launchSpeed;
    private float _leapVelocity;
    private float _leapTimer;

    public bool IsTurnedOff => Def.Type == EnemyDefs.Robot && Session.RobotsOff;
    public string AnimationFrame => _gun?.Frame ?? _animator?.Frame;
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
        if (Def.Type == EnemyDefs.Turret)
        {
            // A fixed platform under a gun head that swivels on its own.
            AddChild(_sprite = new Sprite2D { Texture = Sprites.Get("turret_base") });
            AddChild(_gun = new TurretGun());
            return;
        }
        _sprite = new Sprite2D { Texture = Sprites.ForObject(Def.Type) };
        AddChild(_sprite);
        _animator = new EnemyAnimator(_sprite, Def.Type);
        if (Def.Leaps)
            PrepareLeap();
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
        if (Def.Leaps)
        {
            LeapStep(dt);
            return;
        }
        _sprite.Modulate = Colors.White;

        bool spotted = SeesAgent();
        bool ranged = Def.Attack is EnemyAttack.Pistol or EnemyAttack.MiniGun or EnemyAttack.Plasma;
        float speed = Def.Speed * SpeedUnit * (Session.AlarmActive ? 2f : 1f);   // README: alarm doubles enemy speed

        if (spotted && Def.Attack == EnemyAttack.Contact)
            _facing = Session.Agent.Position.X < Position.X ? -1 : 1;   // chase

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

        // The turret swings its barrel onto the agent before it may fire.
        bool onTarget = _gun?.Track(spotted ? Session.Agent.Position - Position : null, dt) ?? true;
        if (spotted && onTarget && _shootCooldown <= 0)
            Attack();

        _animator?.Update(dt, Velocity, turnedOff: false, _facing);
        CheckContact();
    }

    public void TakeHit()
    {
        if (Submerged)
        {
            Sfx.PlayAt(Sound.BulletHit, Position);   // the shot sinks into the liquid
            return;
        }
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
            _animator?.Attack();
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
                Session.SpawnEnemyBullet(Position, new Vector2(_facing, 0), "tracer");
                break;
            case EnemyAttack.Plasma:
                _shootCooldown = 2f;
                Sfx.PlayAt(Sound.EnemyPlasma, Position);
                if (_gun != null)
                {
                    _gun.Fire();
                    Session.SpawnEnemyBullet(Position + _gun.Muzzle, _gun.Aim, "plasma");
                }
                else
                {
                    Session.SpawnEnemyBullet(Position,
                        (Session.Agent.Position - Position).Normalized(), "plasma");
                }
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

    // A leaper never uses MoveAndSlide: lava and water are not solid, so it
    // would fall through them. It follows its own arc and returns to _home.
    private void PrepareLeap()
    {
        _home = Position;
        int cx = (int)Mathf.Floor(_home.X / 32f);
        int cy = (int)Mathf.Floor(_home.Y / 32f);
        float height = LeapTiles * 32f;
        for (int k = 1; k <= LeapTiles; k++)
        {
            if (!SolidAt(cx, cy - k))
                continue;
            height = Mathf.Max(0f, _home.Y + Def.Body.Position.Y - (cy - k + 1) * 32f);
            break;
        }
        _launchSpeed = Mathf.Sqrt(2f * EnemyGravity * height);
        _leapTimer = LurkTime;
    }

    public bool Submerged => Def.Leaps && _leapPhase == LeapPhase.Lurk && InLiquid;

    private bool InLiquid =>
        Session.TileAt((int)Mathf.Floor(_home.X / 32f), (int)Mathf.Floor(_home.Y / 32f)) is TileDefs.Water or TileDefs.Lava;

    private void LeapStep(float dt)
    {
        if (_leapPhase == LeapPhase.Lurk)
        {
            _leapTimer -= dt * (Session.AlarmActive ? 2f : 1f);   // README: alarm doubles enemy speed
            Position = _home;
            _sprite.Rotation = 0f;
            if (_leapTimer <= 0f && _launchSpeed > 0f)
            {
                _leapPhase = LeapPhase.Leap;
                _leapVelocity = -_launchSpeed;
                if (InLiquid)
                    Sfx.PlayAt(Sound.Swim, Position);
            }
        }
        else
        {
            _leapVelocity += EnemyGravity * dt;
            Position += new Vector2(0f, _leapVelocity * dt);
            if (_leapVelocity > 0f && Position.Y >= _home.Y)
            {
                Position = _home;
                _leapPhase = LeapPhase.Lurk;
                _leapTimer = LurkTime;
            }
            else
            {
                _sprite.Rotation = _facing * (_leapVelocity / _launchSpeed) * Mathf.Pi / 2f;
            }
        }

        _sprite.Modulate = Submerged ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
        _animator.Update(dt, Vector2.Zero, turnedOff: false, _facing);
        if (!Submerged)
            CheckContact();
    }

    private float FrontX => Position.X + (_facing > 0 ? Def.Body.End.X : Def.Body.Position.X);

    private bool SolidAt(int x, int y) => TileDefs.Get(Session.TileAt(x, y))?.Solid == true;
}
