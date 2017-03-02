using SharpNeat.Phenomes;
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
    /// Interaction logic for ConnectFourBoard.xaml
    /// </summary>
    public partial class ConnectFourBoard : UserControl
    {
        const double discSize = 50;
        const double discSpacing = 10;
        const double triangleHeight = 30;
        Dictionary<object, int> polygonCols;

        TextBlock winText;

        Board board;
        double curDiscColor;
        Ellipse[,] discs;

        bool playingGame = false;

        public ConnectFourBoard()
        {
            InitializeComponent();

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
                    new Point(             c * (discSize + discSpacing), 0),
                    new Point(discSize +   c * (discSize + discSpacing), 0),
                    new Point(discSize/2 + c * (discSize + discSpacing), triangleHeight),
                };

                polygonCols.Add(polygon, c);
                polygon.MouseUp += Triangle_MouseDown;
                grid.Children.Add(polygon);
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
                    disc.Margin = new Thickness(c * (discSize + discSpacing),
                                                triangleHeight + discSpacing + r * (discSize + discSpacing),
                                                0, 0);

                    grid.Children.Add(disc);
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
            winText.Margin = new Thickness(0, boardHeight / 2, 0, 0);

            grid.Children.Add(winText);
        }

        public void BeginGame()
        {
            ResetUI();
            board = new Board();
            curDiscColor = Board.YELLOW_DISC;
            playingGame = true;
        }

        private void ResetUI()
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
            if (playingGame)
            {
                int col = polygonCols[sender];
                if (board.CanAddDisc(col))
                {
                    int row = board.RowWhereDiscWillBeAdded(col);
                    double winner = board.AddDisc(curDiscColor, col);
                    discs[row, col].Fill = curDiscColor == Board.YELLOW_DISC ? Brushes.Yellow : Brushes.Red;

                    if (winner != 0)
                    {
                        playingGame = false;
                        winText.Text = winner == Board.YELLOW_DISC ? "Yellow wins!" : "Red wins!";
                        new Thread(delegate ()
                        {
                            Thread.Sleep(1000);
                            grid.Dispatcher.Invoke(BeginGame);

                        }).Start();
                        return;
                    }

                    curDiscColor = -curDiscColor;
                }
            }
        }

        public void VisualizeGame(IBlackBox yellowPlayer, IBlackBox redPlayer)
        {
            //One of them can't play
            if (yellowPlayer == null || redPlayer == null)
                throw new ArgumentException();

            board = new Board();
            curDiscColor = Board.YELLOW_DISC;
            grid.Dispatcher.Invoke(ResetUI);

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
                grid.Dispatcher.Invoke(delegate ()
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
    }
}
