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
        Dictionary<double, bool> isFirstUpdate;

        NeatEvolutionAlgorithm<NeatGenome> yellowTeam;
        NeatEvolutionAlgorithm<NeatGenome> redTeam;

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

            team.Initialize(evaluator, genomeFactory, 50);
        }

        private void OnUpdate(double team)
        {
            //Set event that tells us that we're done evaluating genomes
            doneEvaluatingEvents[team].Set();

            lock(textBlock)
            {
                NeatEvolutionAlgorithm<NeatGenome> alg = team == Board.YELLOW_DISC ? yellowTeam : redTeam;
                AddLine($"{alg.CurrentGeneration}: {alg.Statistics._maxFitness}");
            }

            //Wait for opponent to finish evaluating genomes
            doneEvaluatingEvents[-team].WaitOne();

            //Reset the event that tells us when we're finished generating genomes
            finishedGeneratingEvents[team].Reset();
        }

        private void AddLine(String text)
        {
            textBlock.Dispatcher.BeginInvoke((Action)(()=>textBlock.Text += text + "\r\n"));
        }
    }
}
