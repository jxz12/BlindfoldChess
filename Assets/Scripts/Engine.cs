﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

////////////////////////////////////////////////////////
// a class to save all the information of a chess game

public partial class Engine
{
    private enum Piece { None=0, Pawn, Rook, Knight, Bishop, Queen, King,
                         VirginPawn, VirginRook, VirginKing }; // for castling, en passant etc.

    private Dictionary<int, Piece> whitePieces = new Dictionary<int, Piece>();
    private Dictionary<int, Piece> blackPieces = new Dictionary<int, Piece>();

    public int NRanks { get; private set; } = 8;
    public int NFiles { get; private set; } = 8;
    // for where castled kings go, may be different in variants
    public int LeftCastledFile  { get; private set; } 
    public int RightCastledFile { get; private set; }

    // a class to store all the information needed for a move
    // a Move plus the board state is all the info needed for move generation
    private class Move
    {
        public enum Special { None=0, Normal, Castle, EnPassant };

        public Move previous = null;
        public bool whiteMove = false;
        public int source = 0;
        public int target = 0;
        public Special type = Special.None;
        public Piece moved = Piece.None;
        public Piece captured = Piece.None;
        public Piece promotion = Piece.None;
    }

    // current to evaluate
    private Move prevMove;
    // TODO: private int halfMoveClock;
    private Dictionary<string, Move> legalMoves;

    public Engine(string FEN="rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
                  int leftCastledFile=2, int rightCastledFile=6,
                  bool whitePuush=true, bool blackPuush=true)
    {
        // board = new Board();
        NRanks = FEN.Count(c=>c=='/') + 1;
        NFiles = 0;
        foreach (char c in FEN) // count files in first rank
        {
            if (c == '/') break;
            else if (c > '0' && c <= '9') NFiles += c-'0';
            else NFiles += 1;
        }
        if (NFiles > 26 || NRanks > 9)
            throw new Exception("cannot have more than 26x9 board (blame ASCII lol)");

        int rank = NRanks-1;
        int file = -1;
        int i = 0;
        while (FEN[i] != ' ')
        {
            file += 1;
            int pos = GetPos(rank, file);
            if (FEN[i] == '/')
            {
                if (file != NFiles)
                    throw new Exception("wrong number of squares in FEN rank " + rank);

                rank -= 1;
                file = -1;
            }
            else if (FEN[i] > '0' && FEN[i] <= '9')
            {
                file += FEN[i] - '1'; // -1 because file will be incremented regardless
            }
            else if (FEN[i] == 'P') whitePieces[pos] = (whitePuush && GetRank(pos)==1)
                                                       ? Piece.VirginPawn : Piece.Pawn;
            else if (FEN[i] == 'R') whitePieces[pos] = Piece.Rook;
            else if (FEN[i] == 'N') whitePieces[pos] = Piece.Knight;
            else if (FEN[i] == 'B') whitePieces[pos] = Piece.Bishop;
            else if (FEN[i] == 'Q') whitePieces[pos] = Piece.Queen;
            else if (FEN[i] == 'K') whitePieces[pos] = Piece.VirginKing;
            else if (FEN[i] == 'p') blackPieces[pos] = (blackPuush && GetRank(pos)==NRanks-2)
                                                       ? Piece.VirginPawn : Piece.Pawn;
            else if (FEN[i] == 'r') blackPieces[pos] = Piece.Rook;
            else if (FEN[i] == 'n') blackPieces[pos] = Piece.Knight;
            else if (FEN[i] == 'b') blackPieces[pos] = Piece.Bishop;
            else if (FEN[i] == 'q') blackPieces[pos] = Piece.Queen;
            else if (FEN[i] == 'k') blackPieces[pos] = Piece.VirginKing;
            else throw new Exception("unexpected character " + FEN[i] + " at " + i);

            i += 1;
        }
        prevMove = new Move();

        // 
        i += 1;
        if (FEN[i] == 'w') prevMove.whiteMove = false;
        else if (FEN[i] == 'b') prevMove.whiteMove = true;
        else throw new Exception("unexpected character " + FEN[i] + " at " + i);

        // castling
        i += 2;
        bool K,Q,k,q;
        K=Q=k=q=false;
        while (FEN[i] != ' ')
        {
            if (FEN[i] == 'K') K = true;
            else if (FEN[i] == 'Q') Q = true;
            else if (FEN[i] == 'k') k = true;
            else if (FEN[i] == 'q') q = true;
            else if (FEN[i] == '-') {}
            else throw new Exception("unexpected character " + FEN[i] + " at " + i);

            i += 1;
        }
        foreach (int pos in whitePieces.Keys.ToList())
        {
            if (whitePieces[pos] == Piece.Rook)
            {
                bool leftRook = IsRookLeftCastle(pos, true);
                if (leftRook && K)
                    whitePieces[pos] = Piece.VirginRook;
                if (!leftRook && Q)
                    whitePieces[pos] = Piece.VirginRook;
            }
        }
        foreach (int pos in blackPieces.Keys.ToList())
        {
            if (blackPieces[pos] == Piece.Rook)
            {
                bool leftRook = IsRookLeftCastle(pos, false);
                if (leftRook && k)
                    blackPieces[pos] = Piece.VirginRook;
                if (!leftRook && q)
                    blackPieces[pos] = Piece.VirginRook;
            }
        }

        // en passant
        i += 1;
        if (FEN[i] != '-')
        {
            file = FEN[i] - 'a';
            if (file < 0 || file >= NFiles)
                throw new Exception("unexpected character " + FEN[i] + " at " + i);
            else
            {
                prevMove.moved = Piece.VirginPawn;
                prevMove.source = prevMove.whiteMove? GetPos(1, file) : GetPos(NRanks-2, file);
                prevMove.target = prevMove.whiteMove? GetPos(3, file) : GetPos(NRanks-4, file);
            }
        }

        // TODO: draw counter

        LeftCastledFile = leftCastledFile;
        RightCastledFile = rightCastledFile;

        legalMoves = FindLegalMoves(prevMove);
    }

    public int GetRank(int pos) {
        return pos / NFiles;
    }
    public int GetFile(int pos) {
        return pos % NFiles;
    }
    public int GetPos(int rank, int file) {
        return rank * NFiles + file;
    }
    public bool InBounds(int rank, int file) {
        return file>=0 && file<NFiles && rank>=0 && rank<NRanks;
    }
    public bool Occupied(int pos) {
        return whitePieces.ContainsKey(pos) || blackPieces.ContainsKey(pos);
    }


    ///////////////////////////////////
    // for interface from the outside

    private static Dictionary<Piece, string> pieceStrings = new Dictionary<Piece, string>() {
        { Piece.Pawn, "♟" },
        { Piece.VirginPawn, "♟" },
        { Piece.Rook, "♜" },
        { Piece.VirginRook, "♜" },
        { Piece.Knight, "♞" },
        { Piece.Bishop, "♝" },
        { Piece.Queen, "♛" },
        { Piece.King, "♚" },
        { Piece.VirginKing, "♚" },
    };
    public string PieceOnSquare(int pos, bool white)
    {
        Piece p;
        if (white && whitePieces.TryGetValue(pos, out p))
        {
            return pieceStrings[p];
        }
        else if (!white && blackPieces.TryGetValue(pos, out p))
        {
            return pieceStrings[p];
        }
        return null;
    }

    private string Algebraic(Move move)
    {
        var sb = new StringBuilder();
        if (move.type == Move.Special.Castle)
        {
            if (move.target > move.source) sb.Append('>');
            else sb.Append('<');
        }
        else if (move.moved == Piece.Pawn || move.moved == Piece.VirginPawn)
        {
            sb.Append((char)('a'+(move.source%NFiles)));
            if (move.captured != Piece.None)
            {
                sb.Append('x').Append((char)('a'+(move.target%NFiles)));
            }
            sb.Append(move.target/NFiles + 1);
            if (move.promotion != Piece.None
                && move.promotion != Piece.Pawn)
            {
                sb.Append('=');
                if (move.promotion == Piece.Rook) sb.Append('R');
                else if (move.promotion == Piece.Knight) sb.Append('N');
                else if (move.promotion == Piece.Bishop) sb.Append('B');
                else if (move.promotion == Piece.Queen) sb.Append('Q');
            }
        }
        else
        {
            if (move.moved == Piece.Rook
                || move.moved == Piece.VirginRook) sb.Append('R');
            else if (move.moved == Piece.Knight) sb.Append('N');
            else if (move.moved == Piece.Bishop) sb.Append('B');
            else if (move.moved == Piece.Queen) sb.Append('Q');
            else if (move.moved == Piece.King
                     || move.moved == Piece.VirginKing) sb.Append('K');

            if (move.captured != Piece.None) sb.Append('x');

            sb.Append((char)('a'+(move.target%NFiles)));
            sb.Append(move.target/NFiles + 1);
        }
        return sb.ToString();
    }
    private Dictionary<string, Move> FindLegalMoves(Move prev)
    {
        var nexts = new List<Move>(FindPseudoLegalMoves(prev));
        var ambiguous = new Dictionary<string, List<Move>>();

        foreach (Move next in nexts)
        {
            if (next.type == Move.Special.None)
                continue;

            PlayMove(next);
            var nextnexts = FindPseudoLegalMoves(next);

            // if legal, add to list
            if (!InCheck(next, nextnexts))
            {
                string algebraic = Algebraic(next);
                if (ambiguous.ContainsKey(algebraic))
                {
                    ambiguous[algebraic].Add(next);
                }
                else
                {
                    ambiguous[algebraic] = new List<Move> { next };
                }
            }
            UndoMove(next);
        }
        var unambiguous = new Dictionary<string, Move>();
        foreach (string algebraic in ambiguous.Keys)
        {
            if (ambiguous[algebraic].Count == 1) // already unambiguous
            {
                unambiguous[algebraic] = ambiguous[algebraic][0];
            }
            else
            {
                // ambiguous
                foreach (Move move in ambiguous[algebraic])
                {
                    // check which coordinates (file/rank) clash
                    int file = GetFile(move.source);
                    int rank = GetRank(move.source);
                    bool repeatFile = false;
                    bool repeatRank = false;
                    foreach (Move clash in ambiguous[algebraic].Where(x=> x!=move))
                    {
                        repeatFile |= file == GetFile(clash.source);
                        repeatRank |= rank == GetRank(clash.source);
                    }
                    // if no shared file, use file
                    string disambiguated;
                    if (!repeatFile)
                    {
                        disambiguated = algebraic.Insert(1, ((char)('a'+file)).ToString());
                    }
                    else if (!repeatRank) // use rank
                    {
                        disambiguated = algebraic.Insert(1, ((char)('1'+rank)).ToString());
                    }
                    else // use both
                    {
                        disambiguated = algebraic.Insert(1, ((char)('a'+file)).ToString()
                                                            + ((char)('1'+rank)).ToString());
                    }
                    unambiguous[disambiguated] = move;
                }
            }
        }
        return unambiguous;
    }
    public void PlayMoveAlgebraic(string algebraic)
    {
        Move toPlay;
        if (legalMoves.TryGetValue(algebraic, out toPlay))
        {
            PlayMove(toPlay);
            prevMove = toPlay;
            legalMoves = FindLegalMoves(prevMove);
        }
        else
        {
            throw new Exception("move not legal");
        }
    }
    public IEnumerable<string> GetLegalMovesAlgebraic()
    {
        return legalMoves.Keys;
    }
    public void UndoLastMove()
    {
        if (prevMove != null)
        {
            UndoMove(prevMove);
            prevMove = prevMove.previous;
            legalMoves = FindLegalMoves(prevMove);
        }
        else
        {
            throw new Exception("no moves played yet");
        }
    }
}
