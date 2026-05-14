namespace PVPBack.Core.Realtime.MiniGames.Games.CodeBreakers;

public static class CodeBreakersPuzzleBank
{
    public static readonly List<CodePuzzleDefinition> Puzzles =
    [
        new()
        {
            Code = "4827",
            Hints =
            [
                "First digit is 4.",
                "Second digit is twice the first.",
                "Third digit is smaller than second by 6.",
                "Fourth digit is 5 greater than third."
            ]
        },

        new()
        {
            Code = "5318",
            Hints =
            [
                "First digit is odd.",
                "Second digit is 2 less than first.",
                "Third digit is 1.",
                "Fourth digit is greater than third by 7."
            ]
        },

        new()
        {
            Code = "2468",
            Hints =
            [
                "All digits are even.",
                "First digit is 2.",
                "Each next digit increases by 2.",
                "Last digit is 8."
            ]
        },

        new()
        {
            Code = "7134",
            Hints =
            [
                "First digit is 7.",
                "Second digit is 6 less than first.",
                "Third digit is 2 greater than second.",
                "Fourth digit is 1 greater than third."
            ]
        },

        new()
        {
            Code = "9052",
            Hints =
            [
                "First digit is 9.",
                "Second digit is 0.",
                "Third digit equals second plus 5.",
                "Fourth digit is smaller than third by 3."
            ]
        }
    ];
}