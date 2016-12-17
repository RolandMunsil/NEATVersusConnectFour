﻿using SharpNeat.Core;
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

        Dictionary<double, ManualResetEvent> finishedGeneratingEvents;
        Dictionary<double, AutoResetEvent> doneEvaluatingEvents;
        Dictionary<double, AutoResetEvent> doneShowingStatsEvents;
        Dictionary<double, AutoResetEvent> beginShowingStatsEvents;

        NeatEvolutionAlgorithm<NeatGenome> yellowTeam;
        NeatEvolutionAlgorithm<NeatGenome> redTeam;

        int populationSize = 150;

        private void DoNEAT()
        {
            finishedGeneratingEvents = new Dictionary<double, ManualResetEvent>()
            {
                [Board.YELLOW_DISC] = new ManualResetEvent(false),
                [Board.RED_DISC] = new ManualResetEvent(false)
            };
            doneEvaluatingEvents = new Dictionary<double, AutoResetEvent>()
            {
                [Board.YELLOW_DISC] = new AutoResetEvent(false),
                [Board.RED_DISC] = new AutoResetEvent(false)
            };
            doneShowingStatsEvents = new Dictionary<double, AutoResetEvent>()
            {
                [Board.YELLOW_DISC] = new AutoResetEvent(false),
                [Board.RED_DISC] = new AutoResetEvent(false)
            };
            beginShowingStatsEvents = new Dictionary<double, AutoResetEvent>()
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
            new Thread(ShowStats).Start();

            Thread startYellow = new Thread(() => yellowTeam.StartContinue());
            startYellow.Start();
            redTeam.StartContinue();
            startYellow.Join();
        }

        private NeatEvolutionAlgorithm<NeatGenome> GenerateTeam()
        {
            NeatEvolutionAlgorithmParameters neatParams = new NeatEvolutionAlgorithmParameters();

            IDistanceMetric distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);
            ISpeciationStrategy<NeatGenome> speciationStrategy = new ParallelKMeansClusteringStrategy<NeatGenome>(distanceMetric);

            IComplexityRegulationStrategy complexityStrategy = new NullComplexityRegulationStrategy();

            return new NeatEvolutionAlgorithm<NeatGenome>(neatParams, speciationStrategy, complexityStrategy); 
        }

        private void InitTeam(double teamDisc, NeatEvolutionAlgorithm<NeatGenome> team, NeatEvolutionAlgorithm<NeatGenome> opponent)
        {
            NeatGenomeParameters genomeParams = new NeatGenomeParameters();
            genomeParams.FeedforwardOnly = true;
            IGenomeFactory<NeatGenome> genomeFactory = new NeatGenomeFactory(7 * 6, 7, genomeParams);

            IGenomeDecoder<NeatGenome, IBlackBox> decoder = new NeatGenomeDecoder(SharpNeat.Decoders.NetworkActivationScheme.CreateAcyclicScheme());

            IGenomeListEvaluator<NeatGenome> evaluator = new SerialGenomeListEvaluator<NeatGenome, IBlackBox>(
                decoder, new ConnectFourEvaluator(teamDisc, opponent, decoder, finishedGeneratingEvents[teamDisc], finishedGeneratingEvents[-teamDisc]));

            ManualResetEvent finishedGeneratingEvent = finishedGeneratingEvents[teamDisc];
            team.UpdateEvent += ((sender, eventArgs) => OnUpdate(teamDisc));

            team.Initialize(evaluator, genomeFactory, populationSize);
        }

        private void OnUpdate(double team)
        {
            //Set event that tells us that we're done evaluating genomes
            doneEvaluatingEvents[team].Set();

            //Wait for opponent to finish evaluating genomes
            doneEvaluatingEvents[-team].WaitOne();

            beginShowingStatsEvents[team].Set();
            doneShowingStatsEvents[team].WaitOne();

            //Reset the event that tells us when we're finished generating genomes
            finishedGeneratingEvents[team].Reset();
        }

        private void ShowStats()
        {
            while(true)
            {
                beginShowingStatsEvents[Board.YELLOW_DISC].WaitOne();
                beginShowingStatsEvents[Board.RED_DISC].WaitOne();

                textBlock.Dispatcher.Invoke(delegate()
                {
                    textBlock.Text = $"Generation {redTeam.CurrentGeneration}\r\n" +
                                     $"Yellow Avg Wins {Math.Round(yellowTeam.Statistics._meanFitness)}/{populationSize}\r\n" +
                                     $"Red Avg Wins {Math.Round(redTeam.Statistics._meanFitness)}/{populationSize}\r\n" +
                                     $"Yellow Max Wins {yellowTeam.Statistics._maxFitness}/{populationSize}\r\n" +
                                     $"Red Max Wins {redTeam.Statistics._maxFitness}/{populationSize}\r\n" +
                                     $"Yellow Avg Species Size {yellowTeam.Statistics._minSpecieSize}\r\n" +
                                     $"Red Avg Species Size {redTeam.Statistics._minSpecieSize}\r\n";
                });

                if (redTeam.CurrentGeneration % 50 == 0)
                {
                    if (discs == null)
                        mainGrid.Dispatcher.Invoke(CreateBoardGUI);

                    VisualizeGame((IBlackBox)yellowTeam.CurrentChampGenome.CachedPhenome, (IBlackBox)redTeam.CurrentChampGenome.CachedPhenome);
                    Thread.Sleep(1000);
                }

                //textBlock.Text += $"R {redTeam.CurrentGeneration}: {redTeam.Statistics._meanFitness}\r\n";
                //textBlock.Text += $"Y {yellowTeam.CurrentGeneration}: {yellowTeam.Statistics._meanFitness}\r\n";
                //Line line = new Line();
                //line.X1 = 0;
                //line.X2 = 0;
                //line.Y1 = 100;
                //line.X2 = 30;
                //line.StrokeThickness = 10;
                //line.Stroke = Brushes.Black;
                //plotCanvas.Children.Add(line);

                doneShowingStatsEvents[Board.YELLOW_DISC].Set();
                doneShowingStatsEvents[Board.RED_DISC].Set();
            }
        }

        private void VisualizeGame(IBlackBox yellowPlayer, IBlackBox redPlayer)
        {
            //One of them can't play
            if (yellowPlayer == null || redPlayer == null)
                throw new ArgumentException();

            board = new Board();
            curDiscColor = Board.YELLOW_DISC;
            mainGrid.Dispatcher.Invoke(ResetBoardGUI);

            while (true)
            {
                IBlackBox curPlayer = curDiscColor == Board.YELLOW_DISC ? yellowPlayer : redPlayer;

                double highestActivation = -1;
                int maxCol = -1;
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

                if (!board.CanAddDisc(maxCol))
                {
                    //Whoever is playing has lost
                    winText.Dispatcher.Invoke((Action)delegate ()
                    {
                        winText.Text = curDiscColor == Board.YELLOW_DISC ?
                            $"Red won (because yellow tried to drop in column {maxCol}" :
                            $"Yellow won (because red tried to drop in column {maxCol}";
                    });
                    return;
                }

                int row = board.RowWhereDiscWillBeAdded(maxCol);
                double winner = board.AddDisc(curDiscColor, maxCol);
                mainGrid.Dispatcher.Invoke(delegate ()
                {
                    discs[row, maxCol].Fill = curDiscColor == Board.YELLOW_DISC ? Brushes.Yellow : Brushes.Red;
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
