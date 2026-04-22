namespace Tokito.Services.Games
{
    internal static class GameRatingCalculator
    {
        public static int? Calculate(IEnumerable<int> scores)
        {
            var scoreList = scores.ToList();
            if (scoreList.Count == 0)
            {
                return null;
            }

            return Calculate(scoreList.Average(score => (decimal)score));
        }

        public static int Calculate(decimal averageScore)
        {
            return (int)Math.Round(averageScore, 0, MidpointRounding.AwayFromZero);
        }
    }
}
