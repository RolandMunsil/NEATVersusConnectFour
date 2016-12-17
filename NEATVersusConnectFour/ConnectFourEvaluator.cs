using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NEATVersusConnectFour
{
    class ConnectFourEvaluator : IPhenomeEvaluator<IBlackBox>
    {
        public ulong EvaluationCount
        {
            get;
            private set;
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
                lock (genome)
                {
                    if (opponentPhenome == null)
                    {
                        phenome = genomeDecoder.Decode(genome);
                        genome.CachedPhenome = phenome;
                    }
                }

                double winner;
                if(myTeam == Board.YELLOW_DISC)
                {
                    winner = PlayGame(phenome, opponentPhenome);
                }
                else
                {
                    winner = PlayGame(opponentPhenome, phenome);
                }
                if (winner == myTeam)
                    wins++;
            }

            EvaluationCount++;

            return new FitnessInfo(wins, 0);
            
        }

        private double PlayGame(IBlackBox yellowPlayer, IBlackBox redPlayer)
        {
            //Win because opponent can't play
            if (yellowPlayer == null)
            {
                return Board.RED_DISC;
            }
            else if (redPlayer == null)
            {
                return Board.YELLOW_DISC;
            }

            Board board = new Board();

            int curTurn = 0;
            while (true)
            {
                if (curTurn == (Board.NUM_ROWS * Board.NUM_COLS))
                {
                    //No more pieces to place - no one wins!
                    return 0;
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

                double highestActivation = -1;
                int maxCol = -1;
                lock (curPlayer)
                {
                    curPlayer.InputSignalArray.CopyFrom(board.grid, 0);
                    curPlayer.Activate();

                    for (int i = 0; i < curPlayer.OutputCount; i++)
                    {
                        double activation = curPlayer.OutputSignalArray[i];
                        if (activation > highestActivation)
                        {
                            maxCol = i;
                            highestActivation = activation;
                        }
                    }
                }

                if (!board.CanAddDisc(maxCol))
                {
                    //Whoever is playing has lost
                    return -curDiscColor;
                }
                double winner = board.AddDisc(curDiscColor, maxCol);
                if (winner != 0)
                {
                    return winner;
                }

                curTurn++;
            }
        }

        public void Reset()
        {
        }
    }
}
