namespace HackerOrSoldier;

// One city of the world journey. Each stop names its level file in the
// levels folder; a stop whose file is missing shows "Mission not built yet".
// The campaign is these stops, whatever else sits in the levels folder.
public record CampaignStop(string City, float Lat, float Lon, string Story, string LevelFile);

public static class Campaign
{
    public const string Intro =
        "2030. A syndicate called White Silence has built a machine under the Antarctic ice that can shut down every network on Earth. " +
        "Its root access is split into fifteen admin passwords, each one guarded in a different city. The Agency is sending you. " +
        "Fly city to city, take every password before you leave, and reach the base at the bottom of the world. " +
        "Hacker or soldier, the choice is yours.";

    public const string Outro =
        "The machine goes quiet. Every network on Earth keeps running, and nobody will ever know why. " +
        "The Agency sends its thanks, and a ticket home.";

    // Where the plane leaves from. Not a level.
    public static readonly CampaignStop Origin = new("Reykjavik", 64.15f, -21.94f, "", "");

    public static readonly CampaignStop[] Stops =
    {
        new("London", 51.51f, -0.13f,
            "The trail starts at a bank tower on the Thames. White Silence keeps its first password on a trading floor that never sleeps.", "london.json"),
        new("Bratislava", 48.15f, 17.11f,
            "A quiet server farm on the Danube relays the syndicate's traffic across Europe. The night shift knows nothing. The guards know everything.", "bratislava.json"),
        new("Moscow", 55.75f, 37.62f,
            "Under a ministry building, an old Cold War bunker hums with new hardware. The third password sits behind its blast doors.", "moscow.json"),
        new("Istanbul", 41.01f, 28.98f,
            "Where Europe meets Asia, White Silence launders its money through a shipping company on the Bosphorus. Its records office holds the next password.", "istanbul.json"),
        new("Cairo", 30.04f, 31.24f,
            "A research lab near the pyramids is training the syndicate's machine. Its admin password is the next step on your trail.", "cairo.json"),
        new("Dubai", 25.20f, 55.27f,
            "Ninety floors up, the syndicate's financier runs everything from a glass tower. Get the password and get out before the alarm reaches the ground.", "operation-night-lift.json"),
        new("Mumbai", 19.08f, 72.88f,
            "The machine's code is written in a call centre that answers no calls. Somewhere in its maze of desks hides the seventh password.", "mumbai.json"),
        new("Hong Kong", 22.32f, 114.17f,
            "Container cranes and neon. A harbour warehouse ships the machine's parts south, and its password says where.", "hong-kong.json"),
        new("Tokyo", 35.68f, 139.69f,
            "A robotics plant builds the soldiers that will guard the ice. Take the password and leave before the night shift wakes up.", "tokyo.json"),
        new("Sydney", -33.87f, 151.21f,
            "The supply ships for Antarctica leave from this port. The harbour authority's systems hold the tenth password.", "sydney.json"),
        new("San Francisco", 37.77f, -122.42f,
            "The chip maker that sold White Silence its hardware keeps a backup of every key. Its campus is less friendly than it looks.", "san-francisco.json"),
        new("New York", 40.71f, -74.01f,
            "The syndicate has a seat on the board of a Wall Street firm. The penthouse vault holds the twelfth password.", "new-york.json"),
        new("Mexico City", 19.43f, -99.13f,
            "A cartel guards the syndicate's weapons cache below the old city. The password gets you through without a war, if you can manage it.", "mexico-city.json"),
        new("Rio de Janeiro", -22.91f, -43.17f,
            "The last stop before the ice. A relay on a hillside rooftop talks to the Antarctic base. Take its password and the way south is open.", "rio-de-janeiro.json"),
        new("Antarctica", -76.00f, -58.00f,
            "The base sits under two kilometres of ice. Fourteen passwords brought you to the door. The fifteenth shuts the machine down for good.", "antarctica.json"),
    };

    // The stop a level index maps to, or Origin for the leg out of HQ (-1).
    public static CampaignStop StopAt(int index) => index < 0 ? Origin : Stops[index];

    public static bool IsComplete(int levelIndex) => levelIndex >= Stops.Length;
}
