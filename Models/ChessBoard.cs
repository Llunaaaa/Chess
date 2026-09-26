using System;
using System.Collections.Generic;

namespace Chess.Models;

public enum PieceColor
{
    White,
    Black
}

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

public readonly record struct BoardSquare(int Row, int Column)
{
    public bool IsValid => Row is >= 0 and < 8 && Column is >= 0 and < 8;

    public override string ToString() => $"{(char)('a' + Column)}{8 - Row}";
}

public sealed class ChessPiece
{
    public ChessPiece(PieceColor color, PieceType type)
    {
        Color = color;
        Type = type;
    }

    public PieceColor Color { get; }
    public PieceType Type { get; }
}

public sealed class PromotionContext
{
    public PromotionContext(BoardSquare source, BoardSquare destination, PieceColor color)
    {
        Source = source;
        Destination = destination;
        Color = color;
    }

    public BoardSquare Source { get; }
    public BoardSquare Destination { get; }
    public PieceColor Color { get; }
    public IReadOnlyList<PieceType> Choices { get; } =
        new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
}

public sealed class PromotionRequestedEventArgs : EventArgs
{
    public PromotionRequestedEventArgs(PromotionContext context)
    {
        Context = context;
    }

    public PromotionContext Context { get; }
}

public sealed class MoveResult
{
    private MoveResult(bool succeeded, bool promotionRequired)
    {
        Succeeded = succeeded;
        PromotionRequired = promotionRequired;
    }

    public bool Succeeded { get; }
    public bool PromotionRequired { get; }

    public static MoveResult Invalid { get; } = new(false, false);
    public static MoveResult Moved { get; } = new(true, false);
    public static MoveResult AwaitingPromotion { get; } = new(true, true);
}

public sealed class ChessBoard
{
    public const string InitialPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
    private readonly ChessPiece?[,] squares = new ChessPiece?[8, 8];

    public event EventHandler<PromotionRequestedEventArgs>? PromotionRequested;

    public PromotionContext? PendingPromotion { get; private set; }

    public ChessPiece? this[int row, int column]
    {
        get => row is >= 0 and < 8 && column is >= 0 and < 8 ? squares[row, column] : null;
        private set => squares[row, column] = value;
    }

    public static ChessBoard CreateInitialPosition()
    {
        var board = new ChessBoard();
        board.LoadInitialPosition();
        return board;
    }

    public void LoadInitialPosition()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                this[row, column] = null;
            }
        }

        var backRank = new[]
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (var column = 0; column < 8; column++)
        {
            this[0, column] = new ChessPiece(PieceColor.Black, backRank[column]);
            this[1, column] = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            this[6, column] = new ChessPiece(PieceColor.White, PieceType.Pawn);
            this[7, column] = new ChessPiece(PieceColor.White, backRank[column]);
        }

        PendingPromotion = null;
    }

    public MoveResult TryMove(BoardSquare source, BoardSquare destination)
    {
        if (!source.IsValid || !destination.IsValid || PendingPromotion is not null)
        {
            return MoveResult.Invalid;
        }

        var piece = this[source.Row, source.Column];
        if (piece is null)
        {
            return MoveResult.Invalid;
        }

        var reachesPromotionRank = piece.Type == PieceType.Pawn && (destination.Row == 0 || destination.Row == 7);
        if (reachesPromotionRank)
        {
            var context = new PromotionContext(source, destination, piece.Color);
            PendingPromotion = context;
            PromotionRequested?.Invoke(this, new PromotionRequestedEventArgs(context));
            return MoveResult.AwaitingPromotion;
        }

        this[destination.Row, destination.Column] = piece;
        this[source.Row, source.Column] = null;
        return MoveResult.Moved;
    }

    public bool ChoosePromotion(PieceType pieceType)
    {
        if (PendingPromotion is null || pieceType is PieceType.Pawn or PieceType.King)
        {
            return false;
        }

        var context = PendingPromotion;
        var pawn = this[context.Source.Row, context.Source.Column];
        if (pawn is null || pawn.Type != PieceType.Pawn || pawn.Color != context.Color)
        {
            PendingPromotion = null;
            return false;
        }

        this[context.Destination.Row, context.Destination.Column] = new ChessPiece(context.Color, pieceType);
        this[context.Source.Row, context.Source.Column] = null;
        PendingPromotion = null;
        return true;
    }

    public static string GetPieceAssetUri(ChessPiece piece)
    {
        var color = piece.Color == PieceColor.White ? "white" : "black";
        var type = piece.Type.ToString().ToLowerInvariant();
        return $"ms-appx:///Assets/Pieces/{color}_{type}.svg";
    }
}
