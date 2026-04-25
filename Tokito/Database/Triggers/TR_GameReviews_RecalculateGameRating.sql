CREATE OR ALTER TRIGGER dbo.TR_GameReviews_RecalculateGameRating
ON dbo.GameReviews
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH ChangedGames AS
    (
        SELECT GameId FROM inserted
        UNION
        SELECT GameId FROM deleted
    ),
    RatingByGame AS
    (
        SELECT
            gr.GameId,
            CAST(ROUND(AVG(CAST(gr.Score AS decimal(10,4))), 0) AS int) AS Rating
        FROM dbo.GameReviews gr
        INNER JOIN ChangedGames cg ON cg.GameId = gr.GameId
        GROUP BY gr.GameId
    )
    UPDATE g
    SET g.Rating = r.Rating
    FROM dbo.Games g
    LEFT JOIN RatingByGame r ON r.GameId = g.GameId
    WHERE g.GameId IN (SELECT GameId FROM ChangedGames);
END;
GO
