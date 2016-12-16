using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NEATVersusConnectFour
{
    class Board
    {
        public const double RED_DISC = 1;
        public const double YELLOW_DISC = -1;
        public const double EMPTY = 0;

        public const int numRows = 6;
        public const int numCols = 7;
        public double[] grid;
        private int[] nextSpotInColumn;

        struct Coord
        {
            public int row;
            public int col;

            public Coord(int row, int col)
            {
                this.row = row;
                this.col = col;
            }

            public Coord(int index)
            {
                this.row = index / numCols;
                this.col = index % numCols;
            }
        }

        private double this[Coord coord]
        {
            get
            {
                return grid[coord.col + coord.row * numCols];
            }
        }

        public Board()
        {
            grid = new double[numRows * numCols];
            nextSpotInColumn = new int[numCols];
            for (int c = 0; c < numCols; c++)
            {
                nextSpotInColumn[c] = (numCols * (numRows - 1)) + c;
            }
        }

        public bool CanAddDisc(int column)
        {
            return nextSpotInColumn[column] >= 0;
        }

        /// <returns>0 if no winner, 1 or -1 if there is a winner</returns>
        public double AddDisc(double discColor, int column)
        {
            int index = nextSpotInColumn[column];
            nextSpotInColumn[column] -= numCols;
            grid[index] = discColor;

            //Check for a winner
            Coord newDiscPosition = new Coord(index);
            if (IsHorizontalWin(newDiscPosition, discColor))
                return discColor;
            if (IsVerticalWin(newDiscPosition, discColor))
                return discColor;
            if (IsUpRightDiagonalWin(newDiscPosition, discColor))
                return discColor;
            if (IsUpLeftDiagonalWin(newDiscPosition, discColor))
                return discColor;
            return 0;
        }

        private bool IsHorizontalWin(Coord newDiscCoord, double discColor)
        {
            Coord cur = newDiscCoord;
            while (cur.col >= 0 && this[cur] == discColor)
            {
                cur.col--;
            }
            int leftCol = cur.col + 1;

            cur = newDiscCoord;
            while (cur.col < numCols && this[cur] == discColor)
            {
                cur.col++;
            }
            int rightCol = cur.col;
            return rightCol - leftCol >= 4;
        }

        private bool IsVerticalWin(Coord newDiscCoord, double discColor)
        {
            Coord cur = newDiscCoord;
            while (cur.row >= 0 && this[cur] == discColor)
            {
                cur.row--;
            }
            int topRow = cur.row + 1;

            cur = newDiscCoord;
            while (cur.row < numRows && this[cur] == discColor)
            {
                cur.row++;
            }
            int bottomRow = cur.row;
            return bottomRow - topRow >= 4;
        }

        private bool IsUpRightDiagonalWin(Coord newDiscCoord, double discColor)
        {
            Coord cur = newDiscCoord;
            while (cur.row >= 0 && cur.col >= 0 && this[cur] == discColor)
            {
                cur.row--;
                cur.col--;
            }
            int low = cur.col + 1;

            cur = newDiscCoord;
            while (cur.row < numRows && cur.col < numCols && this[cur] == discColor)
            {
                cur.row++;
                cur.col++;
            }
            int high = cur.col;
            return high - low >= 4;
        }

        private bool IsUpLeftDiagonalWin(Coord newDiscCoord, double discColor)
        {
            Coord cur = newDiscCoord;
            while (cur.row < numRows && cur.col >= 0 && this[cur] == discColor)
            {
                cur.row++;
                cur.col--;
            }
            int low = cur.col + 1;

            cur = newDiscCoord;
            while (cur.row >= 0 && cur.col < numCols && this[cur] == discColor)
            {
                cur.row--;
                cur.col++;
            }
            int high = cur.col;
            return high - low >= 4;
        }
    }
}
