using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monocle
{
    public class VirtualMap<T>
    {
        public const int SegmentSize = 50;
        public readonly int Columns;
        public readonly int Rows;
        public readonly int SegmentColumns;
        public readonly int SegmentRows;
        public readonly T EmptyValue;

        private T[,][,] segments;

        public VirtualMap(int columns, int rows, T emptyValue = default(T))
        {
            Columns = columns;
            Rows = rows;
            SegmentColumns = (columns / SegmentSize) + 1;
            SegmentRows = (rows / SegmentSize) + 1;
            segments = new T[SegmentColumns, SegmentRows][,];
            EmptyValue = emptyValue;
        }

        public VirtualMap(T[,] map, T emptyValue = default(T)) : this(map.GetLength(0), map.GetLength(1), emptyValue)
        {
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    this[x, y] = map[x, y];
        }

        public bool AnyInSegmentAtTile(int x, int y)
        {
            int cx = x / SegmentSize;
            int cy = y / SegmentSize;

            return segments[cx, cy] != null;
        }

        public bool AnyInSegment(int segmentX, int segmentY)
        {
            return segments[segmentX, segmentY] != null;
        }

        public T InSegment(int segmentX, int segmentY, int x, int y)
        {
            return segments[segmentX, segmentY][x, y];
        }

        public T[,] GetSegment(int segmentX, int segmentY)
        {
            return segments[segmentX, segmentY];
        }

        public T SafeCheck(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Columns && y < Rows)
                return this[x, y];
            return EmptyValue;
        }

        public T this[int x, int y]
        {
            get
            {
                if (x < 0 || y < 0 || x >= Columns || y >= Rows)
                    return EmptyValue;

                int cx = x / SegmentSize;
                int cy = y / SegmentSize;

                var seg = segments[cx, cy];
                if (seg == null)
                    return EmptyValue;

                return seg[x - cx * SegmentSize, y - cy * SegmentSize];
            }

            set
            {
                int cx = x / SegmentSize;
                int cy = y / SegmentSize;

                if (segments[cx, cy] == null)
                {
                    segments[cx, cy] = new T[SegmentSize, SegmentSize];

                    // fill with custom Empty Value data
                    if (EmptyValue != null && !EmptyValue.Equals(default(T)))
                        for (int tx = 0; tx < SegmentSize; tx++)
                            for (int ty = 0; ty < SegmentSize; ty++)
                                segments[cx, cy][tx, ty] = EmptyValue;
                }

                segments[cx, cy][x - cx * SegmentSize, y - cy * SegmentSize] = value;
            }
        }

        public T[,] ToArray()
        {
            var array = new T[Columns, Rows];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    array[x, y] = this[x, y];
            return array;
        }

        public VirtualMap<T> Clone()
        {
            var clone = new VirtualMap<T>(Columns, Rows, EmptyValue);
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    clone[x, y] = this[x, y];
            return clone;
        }


	}
	public class VirtualMap3D<T> {
		public const int SegmentSize = 50;
		public readonly int Columns;
		public readonly int Floors;
		public readonly int Rows;
		public readonly int SegmentColumns;
		public readonly int SegmentFloors;
		public readonly int SegmentRows;
		public readonly T EmptyValue;

		private T[,,][,,] segments;

		public VirtualMap3D(int columns, int floors, int rows, T emptyValue = default(T)) {
			Columns = columns;
			Rows = rows;
            Floors = floors;
			SegmentColumns = (columns / SegmentSize) + 1;
			SegmentRows = (rows / SegmentSize) + 1;
			SegmentFloors = (floors / SegmentSize) + 1;
			segments = new T[SegmentColumns, SegmentFloors, SegmentRows][,,];
			EmptyValue = emptyValue;
		}

		public VirtualMap3D(T[,,] map, T emptyValue = default(T)) : this(map.GetLength(0), map.GetLength(1), map.GetLength(2), emptyValue) {
			for (int x = 0; x < Columns; x++)
				for (int y = 0; y < Floors; y++)
				    for (int z = 0; z < Rows; z++)
					    this[x, y, z] = map[x, y, z];
		}

		public bool AnyInSegmentAtTile(int x, int y, int z) {
			int cx = x / SegmentSize;
			int cy = y / SegmentSize;
			int cz = z / SegmentSize;

			return segments[cx, cy, cz] != null;
		}

		public bool AnyInSegment(int segmentX, int segmentY, int segmentZ) {
			return segments[segmentX, segmentY, segmentZ] != null;
		}

		public T InSegment(int segmentX, int segmentY, int segmentZ, int x, int y, int z) {
			return segments[segmentX, segmentY, segmentZ][x, y, z];
		}

		public T[,,] GetSegment(int segmentX, int segmentY, int segmentZ) {
			return segments[segmentX, segmentY, segmentZ];
		}

		public T SafeCheck(int x, int y, int z) {
			if (x >= 0 && y >= 0 && x < Columns && y < Rows)
				return this[x, y, z];
			return EmptyValue;
		}

		public T this[int x, int y, int z] {
			get {
				if (x < 0 || y < 0 || z < 0 || x >= Columns || y >= Floors)
					return EmptyValue;

				int cx = x / SegmentSize;
				int cy = y / SegmentSize;
				int cz = z / SegmentSize;

				var seg = segments[cx, cy, cz];
				if (seg == null)
					return EmptyValue;

				return seg[x - cx * SegmentSize, y - cy * SegmentSize, z - cz * SegmentSize];
			}

			set {
				int cx = x / SegmentSize;
				int cy = y / SegmentSize;
				int cz = z / SegmentSize;

				if (segments[cx, cy, cz] == null) {
					segments[cx, cy, cz] = new T[SegmentSize, SegmentSize, SegmentSize];

					// fill with custom Empty Value data
					if (EmptyValue != null && !EmptyValue.Equals(default(T)))
						for (int tx = 0; tx < SegmentSize; tx++)
							for (int ty = 0; ty < SegmentSize; ty++)
							    for (int tz = 0; tz < SegmentSize; tz++)
								segments[cx, cy, cz][tx, ty, tz] = EmptyValue;
				}

				segments[cx, cy, cz][x - cx * SegmentSize, y - cy * SegmentSize, z - cz * SegmentSize] = value;
			}
		}

		public T[,,] ToArray() {
			var array = new T[Columns, Floors, Rows];
			for (int x = 0; x < Columns; x++)
				for (int y = 0; y < Floors; y++)
				    for (int z = 0; z < Rows; z++)
					array[x, y, z] = this[x, y, z];
			return array;
		}

		public VirtualMap3D<T> Clone() {
			var clone = new VirtualMap3D<T>(Columns, Floors, Rows, EmptyValue);
			for (int x = 0; x < Columns; x++)
				for (int y = 0; y < Floors; y++)
					for (int z = 0; z < Rows; z++)
						clone[x, y, z] = this[x, y, z];
			return clone;
		}


	}
}
