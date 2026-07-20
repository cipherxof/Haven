namespace HavenStudio.Formats.Geo;

public readonly record struct GeoCollisionAttributeDefinition(ulong Flag, string DisplayName);

public static class GeoCollisionAttributes
{
    public const ulong TypeRecoil = 0x2;
    public const ulong Floor = 0x4;
    public const ulong Sound = 0x8;
    public const ulong Player = 0x10;
    public const ulong Enemy = 0x20;
    public const ulong Bullet = 0x40;
    public const ulong Missile = 0x80;
    public const ulong Bomb = 0x100;
    public const ulong Radar = 0x200;
    public const ulong Blood = 0x400;
    public const ulong Ik = 0x800;
    public const ulong Stairway = 0x1000;
    public const ulong StopEye = 0x2000;
    public const ulong Cliff = 0x4000;
    public const ulong TypeThrough = 0x8000;
    public const ulong Lean = 0x10000;
    public const ulong DontFall = 0x20000;
    public const ulong Camera = 0x40000;
    public const ulong Shadow = 0x80000;
    public const ulong Intrude = 0x100000;
    public const ulong AttackGuard = 0x200000;
    public const ulong Rail = 0x400000;
    public const ulong BulletMark = 0x800000;
    public const ulong HeightLimit = 0x1000000;
    public const ulong NoBehind = 0x2000000;
    public const ulong BehindThrough = 0x4000000;
    public const ulong Unknown27 = 0x8000000;
    public const ulong Unknown28 = 0x10000000;
    public const ulong Unknown29 = 0x20000000;
    public const ulong Water = 0x40000000;
    public const ulong Unknown31 = 0x80000000;
    public const ulong Unknown32 = 0x100000000;
    public const ulong Unknown33 = 0x200000000;

    public static readonly GeoCollisionAttributeDefinition[] Definitions =
    [
        new(TypeRecoil, "Type Recoil"),
        new(Floor, "Floor"),
        new(Sound, "Sound"),
        new(Player, "Player"),
        new(Enemy, "Enemy"),
        new(Bullet, "Bullet"),
        new(Missile, "Missile"),
        new(Bomb, "Bomb"),
        new(Radar, "Radar"),
        new(Blood, "Blood"),
        new(Ik, "IK"),
        new(Stairway, "Stairway"),
        new(StopEye, "Stop Eye"),
        new(Cliff, "Cliff"),
        new(TypeThrough, "Type Through"),
        new(Lean, "Lean"),
        new(DontFall, "Don't Fall"),
        new(Camera, "Camera"),
        new(Shadow, "Shadow"),
        new(Intrude, "Intrude"),
        new(AttackGuard, "Attack Guard"),
        new(Rail, "Rail"),
        new(BulletMark, "Bullet Mark"),
        new(HeightLimit, "Height Limit"),
        new(NoBehind, "No Behind"),
        new(BehindThrough, "Behind Through"),
        new(Unknown27, "Flag 27"),
        new(Unknown28, "Flag 28"),
        new(Unknown29, "Flag 29"),
        new(Water, "Water"),
        new(Unknown31, "Flag 31"),
        new(Unknown32, "Flag 32"),
        new(Unknown33, "Flag 33")
    ];

    public static bool MatchesFilter(ulong attributes, ulong? requiredFlag)
    {
        return requiredFlag switch
        {
            null => true,
            0 => attributes == 0,
            var flag => (attributes & flag.Value) != 0
        };
    }
}
