using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using UnityEngine;


// Fast Uniform Dual Contouring attempt
namespace SE.DC
{
	public struct Edge
	{
		public Vector3 A;
		public Vector3 B;
	}

	public struct CellInfo
	{
		public Vector3 Position;
		public Vector3 Lod1Position;
		public Vector3 Normal;
		public Vector3 Lod1Normal;
		public byte Corners;
	}

	public struct CellQEF
	{
		public CellInfo Info;
		public QEF.QEFSolver Qef;
	}

	public static class Algorithm2
	{
		public static List<Vector4> AllCellGizmos = new List<Vector4>();
		public static List<Vector4> BoundaryCellGizmos = new List<Vector4>();
		public static List<Vector4> EdgeCellGizmos = new List<Vector4>();
		public static List<Vector4> MainEdgeCellGizmos = new List<Vector4>();
		public static List<Edge> Edges = new List<Edge>();

		static readonly int[,] FarEdges = { { 3, 7 }, { 5, 7 }, { 6, 7 } };
		static int Resolution = 16;

		public static void Run(int resolution, UtilFuncs.Sampler samp, Chunks.Chunk chunk)
		{
			resolution += 1;
			chunk.State = Chunks.ChunkState.Meshing;
			Resolution = resolution;

			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

			sw.Start();
			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			//List<Vector3> lod1vertices = new List<Vector3>();
			//List<Vector3> lod1normals = new List<Vector3>();
			List<int> indices = new List<int>();

			CellInfo[,,] drawInfos = GenVertices(resolution, samp);
			long genVertsTime = sw.ElapsedMilliseconds; sw.Restart();
			//GenVerticesLOD1(resolution, drawInfos, samp);
			//long genVertsLod1Time = sw.ElapsedMilliseconds; sw.Restart();
			GenIndices(drawInfos, indices, vertices, normals);
			long genIndicesTime = sw.ElapsedMilliseconds;

			chunk.Vertices = vertices;
			chunk.Triangles = indices.ToArray();
			chunk.Normals = normals;
			//chunk.LOD1Vertices = lod1vertices;
			//chunk.LOD1Normals = lod1normals;
			chunk.State = Chunks.ChunkState.Blank; 

			sw.Stop();
			//Debug.Log("Uniform dual contouring time for " + resolution + "^3 mesh: " + (genVertsTime + genVertsLod1Time + genIndicesTime) + "ms" + "(GenVerts: " + genVertsTime + ", GenVertsLOD1: " + genVertsLod1Time + ", GenIndices: " + genIndicesTime + ")");
		}


		public static CellInfo[,,] GenVertices(int resolution, UtilFuncs.Sampler samp)
		{
			CellInfo[,,] cellInfos = new CellInfo[resolution, resolution, resolution];

			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						//Vector3 p = new Vector3(x, y, z);
						byte caseCode = 0;
						sbyte[] densities = new sbyte[8];

						for (int i = 0; i < 8; i++)
						{
							Vector3 pos = new Vector3(x, y, z) + DCC.vfoffsets[i];
							densities[i] = (sbyte)Mathf.Clamp((samp(pos.x, pos.y, pos.z) * 127f), -127f, 127f); //data[index + inArray[i]];

							if (densities[i] < 0) { caseCode |= (byte)(1 << i); }
						}

						Vector3[] cpositions = new Vector3[4];
						Vector3[] cnormals = new Vector3[4];
						int edgeCount = 0;
						for (int i = 0; i < 12 && edgeCount < 4; i++)
						{
							byte c1 = (byte)DCC.edgevmap[i][0];
							byte c2 = (byte)DCC.edgevmap[i][1];

							Vector3 p1 = new Vector3(x, y, z) + DCC.vfoffsets[c1];
							Vector3 p2 = new Vector3(x, y, z) + DCC.vfoffsets[c2];

							bool m1 = ((caseCode >> c1) & 1) == 1;
							bool m2 = ((caseCode >> c2) & 1) == 1;


							if (m1 != m2)
							{
								cpositions[edgeCount] = ApproximateZeroCrossingPosition(p1, p2, samp);
								cnormals[edgeCount] = CalculateSurfaceNormal(cpositions[edgeCount], samp);
								edgeCount++;
							}
						}

						if (edgeCount == 0) continue;

						CellInfo cellInfo = new CellInfo();
						QEF.QEFSolver qef = new QEF.QEFSolver();
						for (int i = 0; i < edgeCount; i++)
						{
							qef.Add(cpositions[i], cnormals[i]);
						}
						cellInfo.Position = qef.Solve(0.0001f, 4, 0.0001f);
						//drawInfo.index = vertices.Count;

						Vector3 max = new Vector3(x, y, z) + Vector3.one;
						if (cellInfo.Position.x < x || cellInfo.Position.x > max.x ||
							cellInfo.Position.y < y || cellInfo.Position.y > max.y ||
							cellInfo.Position.z < z || cellInfo.Position.z > max.z)
						{
							cellInfo.Position = qef.MassPoint;
						}

						//vertices.Add(drawInfo.position);

						for (int i = 0; i < edgeCount; i++)
						{
							cellInfo.Normal += cnormals[i];
						}
						cellInfo.Normal = Vector3.Normalize(cellInfo.Normal); //CalculateSurfaceNormal(drawInfo.position, samp);
						//normals.Add(drawInfo.averageNormal);
						cellInfo.Corners = caseCode;
						cellInfos[x, y, z] = cellInfo;
					}
				}
			}

			return cellInfos;
		}

		public static void GenIndices(CellInfo[,,] cellInfos, List<int> indices, List<Vector3> vertices, List<Vector3> normals)
		{
			int resolution = cellInfos.GetLength(0);
			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						if(x == resolution - 1 || y == resolution - 1 || z == resolution - 1) {
							continue;
						}

						CellInfo cellInfo = cellInfos[x, y, z];
						if (cellInfo.Corners == 0)
						{
							continue;
						}
						byte caseCode = (byte)cellInfo.Corners;
						Vector3 p = new Vector3(x, y, z);

						Vector3Int[][] DCEdgeOffsets = {
							new Vector3Int[] {new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 0), new Vector3Int(0, 1, 1)},
							new Vector3Int[] {new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 0), new Vector3Int(1, 0, 1)},
							new Vector3Int[] {new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0)}
						};

						Vector3Int[] Maxs = {
							new Vector3Int(0, 1, 1), new Vector3Int(1, 0, 1),  new Vector3Int(1, 1, 0)
						};

						CellInfo[] infos = new CellInfo[4];
						infos[0] = cellInfo;

						//int v0 = drawInfo.index;
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{
							Vector3 max = Maxs[edgeNum];
							Vector3Int[] ofs = DCEdgeOffsets[edgeNum];
							if (p.x + max.x >= resolution || p.y + max.y >= resolution || p.z + max.z >= resolution)
							{
								continue;
							}

							int ei0 = FarEdges[edgeNum, 0];
							int ei1 = FarEdges[edgeNum, 1];

							bool edge1 = (caseCode & (1 << ei0)) == (1 << ei0);
							bool edge2 = (caseCode & (1 << ei1)) == (1 << ei1);

							if (edge1 == edge2)
							{
								continue;
							}

							for(int v = 0; v < 3; v++) {
								infos[v + 1] = cellInfos[(int)p.x + ofs[v].x, (int)p.y + ofs[v].y, (int)p.z + ofs[v].z];
							}

							CellInfo[] i1 = { infos[0], infos[1], infos[3] };
							CellInfo[] i2 = { infos[0], infos[3], infos[2] };

							if (((caseCode >> ei0) & 1) == 1 != (edgeNum == 1))
							{
								i1[0] = infos[1]; i1[1] = infos[0];
								i2[0] = infos[3]; i2[1] = infos[0];
							} 
							for (int i = 0; i < 3; i++)
							{
								indices.Add(vertices.Count);
								vertices.Add(i1[i].Position);
								normals.Add(i1[i].Normal);
							}
							for(int i = 0; i < 3; i++) {
								indices.Add(vertices.Count);
								vertices.Add(i2[i].Position);
								normals.Add(i2[i].Normal);
							}
						}
					}
				}
			}
		}


		public static Vector3 Lerp(float density1, float density2, float x1, float y1, float z1, float x2, float y2, float z2)
		{
			if (density1 < 0.00001f && density1 > -0.00001f)
			{
				return new Vector3(x1, y1, z1);
			}
			if (density2 < 0.00001f && density2 > -0.00001f)
			{
				return new Vector3(x2, y2, z2);
			}
			/*if(Mathf.Abs(density1 - density2) < 0.00001f) {
				return new Vector3(x2, y2, z2);
			}*/

			float mu = Mathf.Round((density1) / (density1 - density2) * 256) / 256.0f;

			return new Vector3(x1 + mu * (x2 - x1), y1 + mu * (y2 - y1), z1 + mu * (z2 - z1));
		}
		public static Vector3 LerpN(float density1, float density2, float n1x, float n1y, float n1z, float n2x, float n2y, float n2z)
		{
			float mu = Mathf.Round((density1) / (density1 - density2) * 256f) / 256f;

			return new Vector3(n1x / 127f + mu * (n2x / 127f - n1x / 127f), n1y / 127f + mu * (n2y / 127f - n1y / 127f), n1z / 127f + mu * (n2z / 127f - n1z / 127f));
		}



		public static Vector3 ApproximateZeroCrossingPosition(Vector3 p0, Vector3 p1, UtilFuncs.Sampler sample)
		{
			// approximate the zero crossing by finding the min value along the edge
			float minValue = 100000;
			float t = 0;
			float currentT = 0;
			const int steps = 8;
			const float increment = 1f / (float)steps;
			while (currentT <= 1f)
			{
				Vector3 p = p0 + ((p1 - p0) * currentT);
				float density = Mathf.Abs(sample(p.x, p.y, p.z));
				if (density < minValue)
				{
					minValue = density;
					t = currentT;
				}

				currentT += increment;
			}

			return p0 + ((p1 - p0) * t);
		}

		public static Vector3 CalculateSurfaceNormal(Vector3 p, UtilFuncs.Sampler sample)
		{
			const float H = 0.001f;
			float dx = sample(p.x + H, p.y, p.z) - sample(p.x - H, p.y, p.z);
			float dy = sample(p.x, p.y + H, p.z) - sample(p.x, p.y - H, p.z);
			float dz = sample(p.x, p.y, p.z + H) - sample(p.x, p.y, p.z - H);

			return new Vector3(dx, dy, dz).normalized;
		}
	}
}