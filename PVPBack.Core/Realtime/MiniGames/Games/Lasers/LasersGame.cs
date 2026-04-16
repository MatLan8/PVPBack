using System.Text.Json;
using PVPBack.Core.Realtime.MiniGames.Games.Laser;

namespace PVPBack.Core.Realtime.MiniGames;

public class LaserGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    private readonly Dictionary<string, List<Position>> _playerZones = new();
    private readonly Dictionary<string, int> _playerZoneIndex = new();

    private readonly Dictionary<string, List<Checkpoint>> _checkpoints = new();
    private readonly Dictionary<string, List<Mirror>> _mirrors = new();

    private readonly HashSet<Position> _hitCheckpoints = new();

    private Position _laserStart = null!;
    private Direction _laserDirection;

    private readonly List<LaserStep> _lastLaserPath = new();

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
        _hitCheckpoints.Clear();
        _lastLaserPath.Clear();

        IsCompleted = false;
        IsFailed = false;

        int zoneSize = LaserConstants.GridSize / 2;

        var zones = new List<(int index, List<Position> zone)>
        {
            (0, GetZone(0, 0, zoneSize)),
            (1, GetZone(zoneSize, 0, zoneSize)),
            (2, GetZone(0, zoneSize, zoneSize)),
            (3, GetZone(zoneSize, zoneSize, zoneSize))
        };

        var shuffled = zones.OrderBy(_ => Guid.NewGuid()).ToList();

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var assigned = shuffled[i % 4];

            _playerZones[player.PlayerId] = assigned.zone;
            _playerZoneIndex[player.PlayerId] = assigned.index;

            _mirrors[player.PlayerId] = new List<Mirror>();
        }

        foreach (var player in players)
        {
            var zone = _playerZones[player.PlayerId];

            _checkpoints[player.PlayerId] = zone
                .OrderBy(_ => Guid.NewGuid())
                .Take(LaserConstants.CheckPoints)
                .Select(p => new Checkpoint(p))
                .ToList();
        }

        (_laserStart, _laserDirection) = GenerateRandomLaserStart(new Random());

        ResolveLaser();
        EvaluateCompletion();
        RefreshPlayerPrivateData(players);
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (IsCompleted || IsFailed)
            return Failure("Game already ended.");

        var result = action.Type switch
        {
            "place_mirror" => HandlePlaceMirror(player, action.Data),
            "remove_mirror" => HandleRemoveMirror(player, action.Data),
            _ => Failure("Unknown action.")
        };

        if (!result.Success)
            return result;

        ResolveLaser();
        RefreshPlayerPrivateData(_players);
        
        if (EvaluateCompletion())
        {
            return new GameActionResult
            {
                Success = true,
                Message = "Game completed successfully.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "success",
                    Message = "All checkpoints collected. Lasers game completed!"
                }
            };
        }

        return new GameActionResult
        {
            Success = true,
            Message = result.Message,
            PublicState = GetPublicState()
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

        var pos = new Position(
            data!.Value.GetProperty("x").GetInt32(),
            data.Value.GetProperty("y").GetInt32()
        );

        var type = Enum.Parse<MirrorType>(data.Value.GetProperty("type").GetString()!, true);

        _mirrors[player.PlayerId].Add(new Mirror(pos, type));

        return Ok("Mirror placed.");
    }

    private GameActionResult HandleRemoveMirror(PlayerRuntime player, JsonElement? data)
    {
        if (!_mirrors.ContainsKey(player.PlayerId))
            return Failure("Invalid player.");

        var pos = new Position(
            data!.Value.GetProperty("x").GetInt32(),
            data.Value.GetProperty("y").GetInt32()
        );

        _mirrors[player.PlayerId].RemoveAll(m => m.Position == pos);

        return Ok("Mirror removed.");
    }

    // =====================================================
    // LASER SIMULATION (PURE)
    // =====================================================

    private void ResolveLaser()
    {
        _lastLaserPath.Clear();
        _hitCheckpoints.Clear();

        var pos = _laserStart;
        var dir = _laserDirection;

        for (int step = 0; step < 200; step++)
        {
            _lastLaserPath.Add(new LaserStep(pos, ToAxis(dir)));

            foreach (var cps in _checkpoints.Values.SelectMany(x => x))
            {
                if (cps.Position == pos)
                    _hitCheckpoints.Add(pos);
            }

            var mirror = FindMirrorAt(pos);
            if (mirror != null)
                dir = ApplyMirror(dir, mirror.Type);

            var next = Move(pos, dir);

            if (!IsInside(next))
                break;

            pos = next;
        }
    }

    // =====================================================
    // GAME RULES
    // =====================================================

    private bool EvaluateCompletion()
    {
        var total = _checkpoints.Values.Sum(x => x.Count);

        if (_hitCheckpoints.Count == total)
        {
            IsCompleted = true;
            return true;
        }
        return false;
            
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
                ZoneIndex = zoneIndex,
                ZoneCells = _playerZones.GetValueOrDefault(p.PlayerId, new())
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
            GameType = "Lasers",
            Status = IsFailed ? "failed" : IsCompleted ? "completed" : "running",

            LaserStart = _laserStart,
            LaserDirection = _laserDirection,
            LaserPath = _lastLaserPath,

            Players = _players.Select(p => new
            {
                p.PlayerId,
                MirrorCount = _mirrors[p.PlayerId].Count,
                ZoneIndex = _playerZoneIndex[p.PlayerId]
            }),

            HitCheckpoints = _hitCheckpoints.Count
        };
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static List<Position> GetZone(int ox, int oy, int size)
    {
        var list = new List<Position>();

        for (int x = ox; x < ox + size; x++)
            for (int y = oy; y < oy + size; y++)
                list.Add(new Position(x, y));

        return list;
    }

    private static (Position, Direction) GenerateRandomLaserStart(Random r)
    {
        int i = r.Next(LaserConstants.GridSize);

        return r.Next(4) switch
        {
            0 => (new Position(i, 0), Direction.Down),
            1 => (new Position(i, LaserConstants.GridSize - 1), Direction.Up),
            2 => (new Position(0, i), Direction.Right),
            _ => (new Position(LaserConstants.GridSize - 1, i), Direction.Left),
        };
    }

    private static bool IsInside(Position p)
        => p.X >= 0 && p.X < LaserConstants.GridSize &&
           p.Y >= 0 && p.Y < LaserConstants.GridSize;

    private static LaserAxis ToAxis(Direction dir)
        => (dir == Direction.Up || dir == Direction.Down)
            ? LaserAxis.Vertical
            : LaserAxis.Horizontal;

    private Mirror? FindMirrorAt(Position pos)
        => _mirrors.Values.SelectMany(x => x)
            .FirstOrDefault(m => m.Position == pos);

    private static Direction ApplyMirror(Direction dir, MirrorType type)
        => (dir, type) switch
        {
            (Direction.Up, MirrorType.LeftTurn) => Direction.Left,
            (Direction.Up, MirrorType.RightTurn) => Direction.Right,
            (Direction.Down, MirrorType.LeftTurn) => Direction.Right,
            (Direction.Down, MirrorType.RightTurn) => Direction.Left,
            (Direction.Left, MirrorType.LeftTurn) => Direction.Up,
            (Direction.Left, MirrorType.RightTurn) => Direction.Down,
            (Direction.Right, MirrorType.LeftTurn) => Direction.Down,
            (Direction.Right, MirrorType.RightTurn) => Direction.Up,
            _ => dir
        };

    private static Position Move(Position p, Direction d)
        => d switch
        {
            Direction.Up => new(p.X, p.Y - 1),
            Direction.Down => new(p.X, p.Y + 1),
            Direction.Left => new(p.X - 1, p.Y),
            Direction.Right => new(p.X + 1, p.Y),
            _ => p
        };

    private GameActionResult Ok(string msg)
        => new() { Success = true, Message = msg };

    private GameActionResult Failure(string msg)
        => new() { Success = false, Message = msg };
}