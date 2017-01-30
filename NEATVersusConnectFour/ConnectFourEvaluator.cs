using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NEATVersusConnectFour
{
    class ConnectFourEvaluator : IPhenomeEvaluator<IBlackBox>
    {
        private ulong _evalCount;

        public ulong EvaluationCount
        {
            get { return _evalCount;  }
        }

        public bool StopConditionSatisfied
        {
            get
            {
                return false;
            }
        }

        ManualResetEvent meFinishedGenerating;
        ManualResetEvent opponentFinishedGenerating;
        NeatEvolutionAlgorithm<NeatGenome> opponentTeam;
        IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder;
        double myTeam;

        public static Dictionary<Tuple<IBlackBox, IBlackBox>, double> playedGames = new Dictionary<Tuple<IBlackBox, IBlackBox>, double>();

        public ConnectFourEvaluator(double myTeam, NeatEvolutionAlgorithm<NeatGenome> opponentTeam, IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder, ManualResetEvent meFinishedGenerating, ManualResetEvent opponentFinishedGenerating)
        {
            this.myTeam = myTeam;
            this.meFinishedGenerating = meFinishedGenerating;
            this.opponentFinishedGenerating = opponentFinishedGenerating;
            this.opponentTeam = opponentTeam;
            this.genomeDecoder = genomeDecoder;
        }

        public FitnessInfo Evaluate(IBlackBox phenome)
        {
            meFinishedGenerating.Set();
            //Wait for opponent to finish generating genomes
            opponentFinishedGenerating.WaitOne();

            //Play games against all opponents
            int wins = 0;

            foreach(NeatGenome genome in opponentTeam.GenomeList)
            {
                IBlackBox opponentPhenome = (IBlackBox)genome.CachedPhenome;
                double winner = myTeam == Board.YELLOW_DISC ? PlayGame(phenome, opponentPhenome) : PlayGame(opponentPhenome, phenome);
                if (winner == myTeam)
                    wins++;
            }

            lock(this)
            {
                _evalCount++;
            }

            return new FitnessInfo(wins, 0);
            
        }

        private double PlayGame(IBlackBox yellowPlayer, IBlackBox redPlayer)
        {
            Tuple<IBlackBox, IBlackBox> gameTuple = new Tuple<IBlackBox, IBlackBox>(yellowPlayer, redPlayer);
            double gameWinner;

            //Win because opponent can't play
            if (yellowPlayer == null)
            {
                gameWinner = Board.RED_DISC;
            }
            else if (redPlayer == null)
            {
                gameWinner = Board.YELLOW_DISC;
            }
            else
            {
                lock (playedGames)
                {
                    double winner;
                    if (playedGames.TryGetValue(gameTuple, out winner))
                    {
                        return winner;
                    }
                }

                Board board = new Board();

                int curTurn = 0;
                while (true)
                {
                    if (curTurn == (Board.NUM_ROWS * Board.NUM_COLS))
                    {
                        //No more pieces to place - no one wins!
                        gameWinner = 0;
                        break;
                    }

                    IBlackBox curPlayer;
                    double curDiscColor;
                    if (curTurn % 2 == 0)
                    {
                        curPlayer = yellowPlayer;
                        curDiscColor = Board.YELLOW_DISC;
                    }
                    else
                    {
                        curPlayer = redPlayer;
                        curDiscColor = Board.RED_DISC;
                    }


                    double[] outputs = new double[Board.NUM_COLS];
                    lock (curPlayer)
                    {
                        curPlayer.InputSignalArray.CopyFrom(board.grid, 0);
                        curPlayer.Activate();
                        curPlayer.OutputSignalArray.CopyTo(outputs, 0);                       
                    }

                    int colToAddAt = -1;
                    IEnumerable<int> orderedCols = outputs.Select((a, i) => Tuple.Create(a, i)).OrderByDescending(t => t.Item1).Select(t => t.Item2);
                    foreach (int col in orderedCols)
                    {
                        if (board.CanAddDisc(col))
                        {
                            colToAddAt = col;
                            break;
                        }
                    }

                    double winner = board.AddDisc(curDiscColor, colToAddAt);
                    if (winner != 0)
                    {
                        gameWinner = winner;
                        break;
                    }

                    curTurn++;
                }
            }

            lock (playedGames)
            {
                if (!playedGames.ContainsKey(gameTuple))
                {
                    playedGames.Add(gameTuple, gameWinner);
                }
            }
            return gameWinner;
        }

        public void Reset()
        {
        }
    }
}
