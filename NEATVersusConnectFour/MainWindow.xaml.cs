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

        private void playGameButton_Click(object sender, RoutedEventArgs e)
        {
            cfBoard.BeginGame();
        }

        Barrier doneEvaluatingGenerationBarrier;

        NeatEvolutionAlgorithm<NeatGenome> yellowTeam;
        NeatEvolutionAlgorithm<NeatGenome> redTeam;

        int populationSize = 150;

        private void DoNEAT()
        {
            //TODO: try HyperNEAT - it seems like it would work better for this situation

            doneEvaluatingGenerationBarrier = new Barrier(2, (b)=>ShowStats());

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

            team.UpdateEvent += ((sender, eventArgs) => OnUpdate());

            team.Initialize(evaluator, genomeFactory, populationSize);
        }


        private void OnUpdate()
        {
            doneEvaluatingGenerationBarrier.SignalAndWait();
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
                cfBoard.VisualizeGame((IBlackBox)yellowTeam.CurrentChampGenome.CachedPhenome, (IBlackBox)redTeam.CurrentChampGenome.CachedPhenome);
                Thread.Sleep(1000);
            }

            ConnectFourEvaluator.playedGames.Clear();
        }

        private void ClearText()
        {
            textBlock.Text = "";
        }

        private void AddTextLine(String line)
        {
            textBlock.Text += line + "\r\n";
        }
    }
}
