using System.Text.Json;
using PVPBack.Core.Realtime;
using PVPBack.Core.Realtime.MiniGames.Games.Laser;

namespace PVPBack.Core.Realtime.MiniGames;

public class LaserGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    // Player zones
    private readonly Dictionary<string, List<Position>> _playerZones = new();

    // 0 => top-left, 1 => top-right, 2 => bottom-left, 3 => bottom-right
    private readonly Dictionary<string, int> _playerZoneIndex = new();

    private readonly Dictionary<string, List<Checkpoint>> _checkpoints = new();
    private readonly Dictionary<string, List<Mirror>> _mirrors = new();
    private readonly Dictionary<string, bool> _readyStates = new();

    private readonly HashSet<string> _hitCheckpoints = new();

    private Position _laserStart = null!;
    private Direction _laserDirection;

    private List<LaserStep> _lastLaserPath = new();

    private int _attempts;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // =====================================================
    // START
    // =====================================================

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;

        _playerZones.Clear();
        _playerZoneIndex.Clear();
        _checkpoints.Clear();
        _mirrors.Clear();
        _readyStates.Clear();
        _hitCheckpoints.Clear();
        _lastLaserPath.Clear();

        _attempts = 0;
        IsCompleted = false;
        IsFailed = false;

        var random = new Random();

        // 4 predefined zones
        var zones = new List<(int index, List<Position> zone)>
        {
            (0, GetZone(0, 0)), // top-left
            (1, GetZone(4, 0)), // top-right
            (2, GetZone(0, 4)), // bottom-left
            (3, GetZone(4, 4))  // bottom-right
        };

        var shuffled = zones.OrderBy(_ => Guid.NewGuid()).ToList();

        // Assign zones
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var assigned = shuffled[i % 4];

            _playerZones[player.PlayerId] = assigned.zone;
            _playerZoneIndex[player.PlayerId] = assigned.index;

            _mirrors[player.PlayerId] = new List<Mirror>();
            _readyStates[player.PlayerId] = false;
        }

        // Generate checkpoints (2 per player)
        foreach (var player in players)
        {
            var zone = _playerZones[player.PlayerId];

            _checkpoints[player.PlayerId] = zone
                .OrderBy(_ => Guid.NewGuid())
                .Take(2)
                .Select(p => new Checkpoint(p))
                .ToList();
        }

        // Laser start
        (_laserStart, _laserDirection) = GenerateRandomLaserStart(random);

        RefreshPlayerPrivateData(players);
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (IsCompleted || IsFailed)
            return Failure("Game already ended.");

        return action.Type switch
        {
            "place_mirror" => HandlePlaceMirror(player, action.Data),
            "remove_mirror" => HandleRemoveMirror(player, action.Data),
            "set_ready" => HandleReady(player, action.Data),
            _ => Failure("Unknown action.")
        };
    }

    // =====================================================
    // PRIVATE DATA
    // =====================================================

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var p in players)
        {
            _checkpoints.TryGetValue(p.PlayerId, out var cps);
            _mirrors.TryGetValue(p.PlayerId, out var mirrors);

            _playerZoneIndex.TryGetValue(p.PlayerId, out var zoneIndex);

            p.PrivateData = new
            {
                Checkpoints = cps,
                Mirrors = mirrors,

                LaserStart = _laserStart,
                LaserDirection = _laserDirection,

                LaserPath = _lastLaserPath,

                ZoneIndex = zoneIndex,
                ZoneName = GetZoneName(zoneIndex),
                ZoneCells = _playerZones.TryGetValue(p.PlayerId, out var zone)
                    ? zone
                    : new List<Position>()
            };
        }
    }

    // =====================================================
    // PUBLIC STATE
    // =====================================================

    public object GetPublicState()
    {
        return new
        {
            Status = IsFailed ? "failed" : IsCompleted ? "completed" : "running",
            Attempts = _attempts,
            MaxAttempts = LaserConstants.MaxAttempts,

            Players = _players.Select(p => new
            {
                p.PlayerId,
                IsReady = _readyStates[p.PlayerId],
                MirrorCount = _mirrors[p.PlayerId].Count,
                ZoneIndex = _playerZoneIndex[p.PlayerId]
            }),

            HitCheckpoints = _hitCheckpoints.Count
        };
    }

    // =====================================================
    // MIRRORS
    // =====================================================

    private GameActionResult HandlePlaceMirror(PlayerRuntime player, JsonElement? data)
    {
        if (!_mirrors.ContainsKey(player.PlayerId))
            return Failure("Invalid player.");

        if (_mirrors[player.PlayerId].Count >= LaserConstants.MirrorsPerPlayer)
            return Failure("Mirror limit reached.");

        if (data is null ||
            !data.Value.TryGetProperty("x", out var x) ||
            !data.Value.TryGetProperty("y", out var y) ||
            !data.Value.TryGetProperty("type", out var type))
        {
            return Failure("Invalid payload.");
        }

        var pos = new Position(x.GetInt32(), y.GetInt32());
        var mirrorType = Enum.Parse<MirrorType>(type.GetString()!, true);

        _mirrors[player.PlayerId].Add(new Mirror(pos, mirrorType));

        return Ok("Mirror placed.");
    }

    private GameActionResult HandleRemoveMirror(PlayerRuntime player, JsonElement? data)
    {
        if (!_mirrors.ContainsKey(player.PlayerId))
            return Failure("Invalid player.");

        if (data is null ||
            !data.Value.TryGetProperty("x", out var x) ||
            !data.Value.TryGetProperty("y", out var y))
        {
            return Failure("Invalid payload.");
        }

        var pos = new Position(x.GetInt32(), y.GetInt32());

        _mirrors[player.PlayerId].RemoveAll(m => m.Position == pos);

        return Ok("Mirror removed.");
    }

    // =====================================================
    // READY + RESOLVE
    // =====================================================

    private GameActionResult HandleReady(PlayerRuntime player, JsonElement? data)
    {
        if (!data.HasValue ||
            !data.Value.TryGetProperty("isReady", out var ready))
        {
            return Failure("Invalid ready payload.");
        }

        _readyStates[player.PlayerId] = ready.GetBoolean();

        if (!AllReady())
            return Ok("Player ready updated.");

        return ResolveLaser();
    }

    // =====================================================
    // LASER ENGINE (FULL PATH)
    // =====================================================

    private GameActionResult ResolveLaser()
    {
        _attempts++;

        _lastLaserPath.Clear();
        _hitCheckpoints.Clear();

        var pos = _laserStart;
        var dir = _laserDirection;

        for (int step = 0; step < 200; step++)
        {
            // SAVE STEP (IMPORTANT)
            _lastLaserPath.Add(new LaserStep(pos, ToAxis(dir)));

            // CHECKPOINT HIT
            foreach (var cps in _checkpoints.Values.SelectMany(x => x))
            {
                if (cps.Position == pos)
                    _hitCheckpoints.Add($"{pos.X},{pos.Y}");
            }

            // MIRROR LOGIC
            var mirror = FindMirrorAt(pos);

            if (mirror != null)
                dir = ApplyMirror(dir, mirror.Type);

            // MOVE FORWARD (or straight if no mirror)
            var next = Move(pos, dir);

            if (!IsInside(next))
            {
                _lastLaserPath.Add(new LaserStep(next, ToAxis(dir)));
                break;
            }

            pos = next;
        }

        var totalCheckpoints = _checkpoints.Values.Sum(x => x.Count);

        if (_hitCheckpoints.Count == totalCheckpoints)
        {
            IsCompleted = true;
            return Ok("All checkpoints hit. Game completed.");
        }

        if (_attempts >= LaserConstants.MaxAttempts)
        {
            IsFailed = true;
            return Failure("No attempts left. Game failed.");
        }

        ResetReady();

        return new GameActionResult
        {
            Success = true,
            Message = "Attempt failed.",
            PublicState = GetPublicState()
        };
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static LaserAxis ToAxis(Direction dir)
        => (dir == Direction.Up || dir == Direction.Down)
            ? LaserAxis.Vertical
            : LaserAxis.Horizontal;

    private Mirror? FindMirrorAt(Position pos)
        => _mirrors.Values.SelectMany(x => x)
            .FirstOrDefault(m => m.Position == pos);

    private static Direction ApplyMirror(Direction dir, MirrorType type)
    {
        return (dir, type) switch
        {
            (Direction.Up, MirrorType.LeftTurn) => Direction.Left,
            (Direction.Up, MirrorType.RightTurn) => Direction.Right,

            (Direction.Down, MirrorType.LeftTurn) => Direction.Right,
            (Direction.Down, MirrorType.RightTurn) => Direction.Left,

            (Direction.Left, MirrorType.LeftTurn) => Direction.Up,      // was Down
            (Direction.Left, MirrorType.RightTurn) => Direction.Down,    // was Up
            
            (Direction.Right, MirrorType.LeftTurn) => Direction.Down,    // was Up
            (Direction.Right, MirrorType.RightTurn) => Direction.Up,     // was Down

            _ => dir
        };
    }

    private static Position Move(Position p, Direction d)
    {
        return d switch
        {
            Direction.Up => new Position(p.X, p.Y - 1),
            Direction.Down => new Position(p.X, p.Y + 1),
            Direction.Left => new Position(p.X - 1, p.Y),
            Direction.Right => new Position(p.X + 1, p.Y),
            _ => p
        };
    }

    private static bool IsInside(Position p)
        => p.X >= 0 && p.X < LaserConstants.GridSize &&
           p.Y >= 0 && p.Y < LaserConstants.GridSize;

    private static (Position, Direction) GenerateRandomLaserStart(Random r)
    {
        int side = r.Next(4);
        int i = r.Next(8);

        return side switch
        {
            0 => (new Position(i, 0), Direction.Down),
            1 => (new Position(i, 7), Direction.Up),
            2 => (new Position(0, i), Direction.Right),
            _ => (new Position(7, i), Direction.Left),
        };
    }

    private static List<Position> GetZone(int ox, int oy)
    {
        var list = new List<Position>();

        for (int x = ox; x < ox + 2; x++)
            for (int y = oy; y < oy + 2; y++)
                list.Add(new Position(x, y));

        return list;
    }

    private static string GetZoneName(int index)
        => index switch
        {
            0 => "top-left",
            1 => "top-right",
            2 => "bottom-left",
            3 => "bottom-right",
            _ => "unknown"
        };

    private bool AllReady() => _readyStates.Values.All(x => x);

    private void ResetReady()
    {
        foreach (var k in _readyStates.Keys.ToList())
            _readyStates[k] = false;
    }

    private GameActionResult Ok(string msg)
        => new() { Success = true, Message = msg, PublicState = GetPublicState() };

    private GameActionResult Failure(string msg)
        => new() { Success = false, Message = msg, PublicState = GetPublicState() };
}