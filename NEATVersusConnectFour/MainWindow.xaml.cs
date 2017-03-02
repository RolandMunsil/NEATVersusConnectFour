using SharpNeat.Core;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;
using SharpNeat.SpeciationStrategies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NEATVersusConnectFour
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void startNEATButton_Click(object sender, RoutedEventArgs e)
        {
            (new Thread(DoNEAT)).Start();
        }

        Dictionary<double, AutoResetEvent> doneShowingStatsEvents;

        Barrier doneEvaluatingGenerationBarrier;

        NeatEvolutionAlgorithm<NeatGenome> yellowTeam;
        NeatEvolutionAlgorithm<NeatGenome> redTeam;

        int populationSize = 150;

        private void DoNEAT()
        {
            //TODO: try HyperNEAT - it seems like it would work better for this situation

            mainGrid.Dispatcher.Invoke(CreateBoardGUI);

            doneEvaluatingGenerationBarrier = new Barrier(2, (b)=>ShowStats());

            doneShowingStatsEvents = new Dictionary<double, AutoResetEvent>()
            {
                [Board.YELLOW_DISC] = new AutoResetEvent(false),
                [Board.RED_DISC] = new AutoResetEvent(false)
            };
            yellowTeam = GenerateTeam();
            redTeam = GenerateTeam();

            Thread initYellow = new Thread(() => InitTeam(Board.YELLOW_DISC, yellowTeam, redTeam));
            initYellow.Start();
            InitTeam(Board.RED_DISC, redTeam, yellowTeam);
            initYellow.Join();

            yellowTeam.UpdateScheme = new UpdateScheme(1);
            redTeam.UpdateScheme = new UpdateScheme(1);

            Thread startYellow = new Thread(() => yellowTeam.StartContinue());
            startYellow.Start();
            redTeam.StartContinue();
            startYellow.Join();
        }

        private NeatEvolutionAlgorithm<NeatGenome> GenerateTeam()
        {
            NeatEvolutionAlgorithmParameters neatParams = new NeatEvolutionAlgorithmParameters();

            IDistanceMetric distanceMetric = new ManhattanDistanceMetric(0.4, 1.0, 0.0);
            ISpeciationStrategy<NeatGenome> speciationStrategy = new ParallelKMeansClusteringStrategy<NeatGenome>(distanceMetric);

            IComplexityRegulationStrategy complexityStrategy = new NullComplexityRegulationStrategy();

            return new NeatEvolutionAlgorithm<NeatGenome>(neatParams, speciationStrategy, complexityStrategy); 
        }

        private void InitTeam(double teamDisc, NeatEvolutionAlgorithm<NeatGenome> team, NeatEvolutionAlgorithm<NeatGenome> opponent)
        {
            NeatGenomeParameters genomeParams = new NeatGenomeParameters();
            genomeParams.FeedforwardOnly = true;
            //genomeParams.InitialInterconnectionsProportion = 1.0;

            IGenomeFactory<NeatGenome> genomeFactory = new NeatGenomeFactory(7 * 6, 7, genomeParams);

            IGenomeDecoder<NeatGenome, IBlackBox> decoder = new NeatGenomeDecoder(SharpNeat.Decoders.NetworkActivationScheme.CreateAcyclicScheme());

            IGenomeListEvaluator<NeatGenome> evaluator = new CacheFirstParallelGenomeListEvaluator<NeatGenome, IBlackBox>(
                decoder, new ConnectFourEvaluator(teamDisc, opponent, decoder));

            team.UpdateEvent += ((sender, eventArgs) => OnUpdate(teamDisc));

            team.Initialize(evaluator, genomeFactory, populationSize);
        }


        private void OnUpdate(double team)
        {
            doneEvaluatingGenerationBarrier.SignalAndWait();

            doneShowingStatsEvents[team].WaitOne();
        }

        private void ShowStats()
        {
            int bothPhenomesNull = (populationSize * populationSize) - ConnectFourEvaluator.playedGames.Count;
            int redWins = 0;
            int yellowWins = 0;
            int ties = 0;

            foreach (double d in ConnectFourEvaluator.playedGames.Values)
            {
                if(d == Board.RED_DISC)
                    redWins++;
                else if (d == Board.YELLOW_DISC)
                    yellowWins++;
                else if (d == 0)
                    ties++;
                else
                    throw new Exception();
            }

#if DEBUG
            List<IBlackBox> yellows = ConnectFourEvaluator.playedGames.Keys.Select(t => t.Item1).Distinct().ToList();
            int numYellows = yellows.Count;
            List<IBlackBox> reds = ConnectFourEvaluator.playedGames.Keys.Select(t => t.Item2).Distinct().ToList();
            int numReds = reds.Count;
            if (numYellows != 150)
                Debugger.Break();
            if (numReds != 150)
                Debugger.Break();

            if (bothPhenomesNull < 0)
                Debugger.Break();

            if (redWins != redTeam.GenomeList.Sum(g => g.EvaluationInfo.Fitness))
                Debugger.Break();
            if (yellowWins != yellowTeam.GenomeList.Sum(g => g.EvaluationInfo.Fitness))
                Debugger.Break();
#endif

            textBlock.Dispatcher.Invoke(delegate()
            {
                ClearText();
                AddTextLine($"Generation {redTeam.CurrentGeneration}");
                AddTextLine($"Yellow Avg Wins {Math.Round(yellowTeam.Statistics._meanFitness)}/{populationSize}");
                AddTextLine($"Red Avg Wins {Math.Round(redTeam.Statistics._meanFitness)}/{populationSize}");
                AddTextLine($"Yellow Max Wins {yellowTeam.Statistics._maxFitness}/{populationSize}");
                AddTextLine($"Red Max Wins {redTeam.Statistics._maxFitness}/{populationSize}");
                AddTextLine($"{yellowWins} | {redWins} | {ties}+{bothPhenomesNull}");
            });

            if (redTeam.CurrentGeneration % 20 == 0)
            {
                VisualizeGame((IBlackBox)yellowTeam.CurrentChampGenome.CachedPhenome, (IBlackBox)redTeam.CurrentChampGenome.CachedPhenome);
                Thread.Sleep(1000);
            }

            ConnectFourEvaluator.playedGames.Clear();

            doneShowingStatsEvents[Board.YELLOW_DISC].Set();
            doneShowingStatsEvents[Board.RED_DISC].Set();
        }

        private void ClearText()
        {
            textBlock.Text = "";
        }

        private void AddTextLine(String line)
        {
            textBlock.Text += line + "\r\n";
        }

        private void VisualizeGame(IBlackBox yellowPlayer, IBlackBox redPlayer)
        {
            //One of them can't play
            if (yellowPlayer == null || redPlayer == null)
                throw new ArgumentException();

            board = new Board();
            curDiscColor = Board.YELLOW_DISC;
            mainGrid.Dispatcher.Invoke(ResetBoardGUI);

            int curTurn = 0;
            while (true)
            {
                if (curTurn == (Board.NUM_ROWS * Board.NUM_COLS))
                {
                    //No more pieces to place - no one wins!
                    winText.Dispatcher.Invoke((Action)delegate ()
                    {
                        winText.Text = "Tie!";
                    });
                    return;
                }

                IBlackBox curPlayer = curDiscColor == Board.YELLOW_DISC ? yellowPlayer : redPlayer;

                int colToAddAt = -1;

                double[] outputs = new double[curPlayer.OutputCount];
                curPlayer.OutputSignalArray.CopyTo(outputs, 0);
                IEnumerable<int> orderedCols = outputs.Select((a, i) => Tuple.Create(a, i)).OrderByDescending(t => t.Item1).Select(t => t.Item2);
                foreach (int col in orderedCols)
                {
                    if (board.CanAddDisc(col))
                    {
                        colToAddAt = col;
                        break;
                    }
                }

                //if (!board.CanAddDisc(colToAddAt))
                //{
                //    //Whoever is playing has lost
                //    winText.Dispatcher.Invoke((Action)delegate ()
                //    {
                //        winText.Text = curDiscColor == Board.YELLOW_DISC ?
                //            $"Red won (because yellow tried to drop in column {maxCol}" :
                //            $"Yellow won (because red tried to drop in column {maxCol}";
                //    });
                //    return;
                //}

                int row = board.RowWhereDiscWillBeAdded(colToAddAt);
                double winner = board.AddDisc(curDiscColor, colToAddAt);
                mainGrid.Dispatcher.Invoke(delegate ()
                {
                    discs[row, colToAddAt].Fill = curDiscColor == Board.YELLOW_DISC ? Brushes.Yellow : Brushes.Red;
                });

                if (winner != 0)
                {
                    winText.Dispatcher.Invoke((Action)delegate ()
                    {
                        winText.Text = curDiscColor == Board.YELLOW_DISC ? $"Yellow won!" : $"Red won!";
                    });
                    return;
                }
                Thread.Sleep(500);
                curDiscColor = -curDiscColor;
                curTurn++;
            }
        }

        double discSize = 50;
        double discSpacing = 10;
        double triangleHeight = 30;
        Vector boardTopLeft = new Vector(100, 150);
        Dictionary<object, int> polygonCols;

        TextBlock winText;

        Board board;
        double curDiscColor;
        Ellipse[,] discs;

        private void playGameButton_Click(object sender, RoutedEventArgs e)
        {
            CreateBoardGUI();

            board = new Board();
            curDiscColor = Board.YELLOW_DISC;
        }

        private void CreateBoardGUI()
        {
            polygonCols = new Dictionary<object, int>();

            for (int c = 0; c < Board.NUM_COLS; c++)
            {
                Polygon polygon = new Polygon();
                polygon.Fill = Brushes.SlateGray;
                polygon.StrokeThickness = 0;
                polygon.HorizontalAlignment = HorizontalAlignment.Left;
                polygon.VerticalAlignment = VerticalAlignment.Top;
                polygon.Points = new PointCollection()
                {
                    boardTopLeft + new Point(             c * (discSize + discSpacing), 0),
                    boardTopLeft + new Point(discSize +   c * (discSize + discSpacing), 0),
                    boardTopLeft + new Point(discSize/2 + c * (discSize + discSpacing), triangleHeight),
                };

                polygonCols.Add(polygon, c);
                polygon.MouseUp += Triangle_MouseDown;
                mainGrid.Children.Add(polygon);
            }

            discs = new Ellipse[Board.NUM_ROWS, Board.NUM_COLS];

            //Construct circles
            for (int r = 0; r < Board.NUM_ROWS; r++)
            {
                for (int c = 0; c < Board.NUM_COLS; c++)
                {
                    Ellipse disc = new Ellipse();
                    disc.Height = discSize;
                    disc.Width = discSize;
                    disc.Fill = Brushes.LightGray;
                    disc.StrokeThickness = 0;
                    disc.HorizontalAlignment = HorizontalAlignment.Left;
                    disc.VerticalAlignment = VerticalAlignment.Top;
                    disc.Margin = new Thickness(boardTopLeft.X + c * (discSize + discSpacing),
                                                boardTopLeft.Y + triangleHeight + discSpacing + r * (discSize + discSpacing),
                                                0, 0);

                    mainGrid.Children.Add(disc);
                    discs[r, c] = disc;
                }
            }

            winText = new TextBlock();
            winText.Text = "";
            winText.TextAlignment = TextAlignment.Center;
            winText.HorizontalAlignment = HorizontalAlignment.Left;
            winText.VerticalAlignment = VerticalAlignment.Top;
            winText.FontSize = 48;

            double boardWidth = (discSize * Board.NUM_COLS) + (discSpacing * (Board.NUM_COLS - 1));
            double boardHeight = (discSize * Board.NUM_ROWS) + (discSpacing * (Board.NUM_ROWS - 1))
                                 + triangleHeight + discSpacing;

            winText.Width = boardWidth;
            winText.Margin = new Thickness(boardTopLeft.X, boardTopLeft.Y + boardHeight / 2, 0, 0);

            mainGrid.Children.Add(winText);
        }

        private void ResetBoardGUI()
        {
            for (int r = 0; r < Board.NUM_ROWS; r++)
            {
                for (int c = 0; c < Board.NUM_COLS; c++)
                {
                    discs[r, c].Fill = Brushes.LightGray;
                }
            }

            winText.Text = "";
        }

        private void Triangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int col = polygonCols[sender];
            if (board.CanAddDisc(col))
            {
                int row = board.RowWhereDiscWillBeAdded(col);
                double winner = board.AddDisc(curDiscColor, col);
                discs[row, col].Fill = curDiscColor == Board.YELLOW_DISC ? Brushes.Yellow : Brushes.Red;

                if (winner != 0)
                {
                    winText.Text = winner == Board.YELLOW_DISC ? "Yellow wins!" : "Red wins!";
                    new Thread(delegate()
                    {
                        Thread.Sleep(1000);
                        mainGrid.Dispatcher.BeginInvoke((Action)ResetBoardGUI);
                        board = new Board();
                        curDiscColor = Board.YELLOW_DISC;

                    }).Start();
                    return;
                }

                curDiscColor = -curDiscColor;
            }
        }
    }
}
