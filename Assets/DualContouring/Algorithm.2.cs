using System.Collections.Generic;
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

	public static class Algorithm2
	{
		public static List<Vector4> AllCellGizmos = new List<Vector4>();
		public static List<Vector4> BoundaryCellGizmos = new List<Vector4>();
		public static List<Vector4> EdgeCellGizmos = new List<Vector4>();
		public static List<Vector4> MainEdgeCellGizmos = new List<Vector4>();
		public static List<Edge> Edges = new List<Edge>();


		public static readonly Vector3[] CHILD_MIN_OFFSETS = {
			new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 1, 1),
			new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 0), new Vector3(1, 1, 1)
		};

		static readonly int[,] FarEdges = { { 3, 7 }, { 5, 7 }, { 6, 7 } };

		public static Mesh Run(int resolution, UtilFuncs.Sampler samp, Chunks.ChunkNode node, float scaleFactor)
		{
			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

			sw.Start();
			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();

			OctreeDrawInfo[,,] drawInfos = GenVertices(resolution, vertices, normals, samp, scaleFactor);
			Mesh m = GenMesh(resolution, drawInfos, vertices, normals);

			node.drawInfos = drawInfos;

			sw.Stop();
			//Debug.Log("Fast uniform dual contouring time for " + resolution + "^3 mesh: " + sw.ElapsedMilliseconds + "ms");

			return m;
		}

		public static OctreeDrawInfo[,,] GenVertices(int resolution, List<Vector3> vertices, List<Vector3> normals, UtilFuncs.Sampler samp, float scaleFactor)
		{
			int[,,] vIds = new int[resolution, resolution, resolution];

			OctreeDrawInfo[,,] drawInfos = new OctreeDrawInfo[resolution, resolution, resolution];

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
							Vector3 pos = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[i];
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

							Vector3 p1 = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c1];
							Vector3 p2 = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c2];

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

						SE.DC.OctreeDrawInfo drawInfo = new OctreeDrawInfo();
						QEF.QEFSolver qef = new QEF.QEFSolver();
						drawInfo.qef = qef;
						for (int i = 0; i < edgeCount; i++)
						{
							qef.Add(cpositions[i], cnormals[i]);
						}
						drawInfo.position = qef.Solve(0.0001f, 4, 0.0001f);
						drawInfo.index = vertices.Count;

						Vector3 max = new Vector3(x, y, z) + Vector3.one;
						if (drawInfo.position.x < x || drawInfo.position.x > max.x ||
							drawInfo.position.y < y || drawInfo.position.y > max.y ||
							drawInfo.position.z < z || drawInfo.position.z > max.z)
						{
							drawInfo.position = drawInfo.qef.MassPoint * scaleFactor;
						}

						vertices.Add(drawInfo.position * scaleFactor);

						for (int i = 0; i < edgeCount; i++)
						{
							drawInfo.averageNormal += cnormals[i];
						}
						drawInfo.averageNormal = Vector3.Normalize(drawInfo.averageNormal); //CalculateSurfaceNormal(drawInfo.position, samp);
						normals.Add(drawInfo.averageNormal);
						drawInfo.corners = caseCode;
						drawInfos[x, y, z] = drawInfo;
					}
				}
			}

			return drawInfos;
		}

		public static int worldSize = 16;

		public static Mesh GenSeamMesh(int resolution, Chunks.ChunkNode ogNode, Chunks.ChunkNode[] seamNodes)
		{
			Debug.Log("Seam ogNode: " + ogNode);

			Mesh m = new Mesh();

			return m;

			int resm1 = resolution - 1;

			List<Vector3> normals = new List<Vector3>();
			List<Vector3> vertices = new List<Vector3>();
			List<int> indices = new List<int>();

			//iterate through all cells of ogNode
			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						Vector3 p = new Vector3(x, y, z);

						// if any coord is not a max, skip voxel
						if (x != resm1 && y != resm1 && z != resm1)
						{
							continue;
						}


						if (ogNode.drawInfos[x, y, z] == null) continue;

						OctreeDrawInfo info = ogNode.drawInfos[x, y, z];

						float nodeSize = ogNode.Size * worldSize;
						float cellsize = nodeSize / (float)resolution;

						Vector3 center = new Vector3(x, y, z) * cellsize + (Vector3.one * (cellsize / 2)) + ogNode.Position * worldSize;
						BoundaryCellGizmos.Add(new Vector4(center.x, center.y, center.z, cellsize));

						// cell is max
						byte maxMask = 0;
						if (x == resm1) maxMask |= 1;
						if (y == resm1) maxMask |= 2;
						if (z == resm1) maxMask |= 4;

						byte caseCode = (byte)info.corners;

						// go through 3 far edges of voxel
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{

							int ei0 = FarEdges[edgeNum, 0];
							int ei1 = FarEdges[edgeNum, 1];

							Vector3 c0 = p + CHILD_MIN_OFFSETS[ei0];
							Vector3 c1 = p + CHILD_MIN_OFFSETS[ei1];

							// check if edge is maximal
							// an edge is maximal if both its vertices
							// have at least one coordinate that is res - 1
							if (!((c0.x == resolution || c0.y == resolution || c0.z == resolution) && (c1.x == resolution || c1.y == resolution || c1.z == resolution)))
							{
								Debug.Log("Non-maximal edge C0: " + c0 + ", C1: " + c1);
								continue; // edge is not maximal
							}

							bool edge1 = (caseCode & (1 << ei0)) == (1 << ei0);
							bool edge2 = (caseCode & (1 << ei1)) == (1 << ei1);

							if (edge1 == edge2)
							{
								continue;
							}

							Edge e = new Edge();
							e.A = c0 * cellsize + ogNode.Position * worldSize;
							e.B = c1 * cellsize + ogNode.Position * worldSize;
							Edges.Add(e);

							//Debug.Log("Creating triangle in seam mesh + ogNode: " + ogNode);
							//Debug.Log("Creating triangle in seam mesh + ogNode drawinfos: " + ogNode.drawInfos);

							// edge is maximal
							// now to generate a quad around the edge
							// first have to get the 4 vert ids
							Vector3[] vs = new Vector3[4];
							Vector3[] ns = new Vector3[4];

							Debug.Log("Got here");

							OctreeDrawInfo ogDrawInfo = ogNode.drawInfos[x, y, z];

							vs[0] = ogDrawInfo.position;
							ns[0] = ogDrawInfo.averageNormal;

							MainEdgeCellGizmos.Add(new Vector4(center.x, center.y, center.z, cellsize));

							Vector3[][] DCEdgeoffsets = {
								new Vector3[] {new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 1, 1)},
								new Vector3[] {new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 0, 1)},
								new Vector3[] {new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0)}
							};

							Vector3[] DCOffsets = DCEdgeoffsets[edgeNum];
							for (int vertNum = 0; vertNum < 3; vertNum++)
							{
								byte mask = 0;
								Vector3 offset = DCOffsets[vertNum] + p;
								offset = wrap(offset, ref mask, resolution);

								Vector3 ee = Vector3.zero;

								if ((mask & 1) == 1) ee.x += resolution;
								if ((mask & 2) == 2) ee.y += resolution;
								if ((mask & 4) == 4) ee.z += resolution;


								Debug.Log("offset: " + DCOffsets[vertNum] + " (" + offset + ") " + ", mask: " + mask);
								Chunks.ChunkNode targetChunk;
								if (mask != 0)
								{
									Debug.Log("Target chunk is other chunk");
									targetChunk = seamNodes[mask - 1];
									//ee = resolution * DCOffsets[vertNum];
								}
								else
								{
									Debug.Log("Target chunk is own chunk, offset: " + offset);
									targetChunk = ogNode;
								}
								center = offset * cellsize + (Vector3.one * cellsize * 0.5f) + ogNode.Position * worldSize + ee * cellsize;
								//EdgeCellGizmos.Add(new Vector4(center.x, center.y, center.z, cellsize));

								vs[vertNum + 1] = targetChunk.drawInfos[(int)offset.x, (int)offset.y, (int)offset.z].position + ee;
								ns[vertNum + 1] = targetChunk.drawInfos[(int)offset.x, (int)offset.y, (int)offset.z].averageNormal;
							}


							int[] tri1 = { 0, 1, 3 };
							int[] tri2 = { 0, 3, 2 };
							if (((ogDrawInfo.corners >> ei0) & 1) == 1 != (edgeNum == 1))
							{ // flip
								tri1[0] = 1; tri1[1] = 0;
								tri2[0] = 3; tri2[1] = 0;
							}

							for (int i = 0; i < 3; i++)
							{
								vertices.Add(vs[tri1[i]]);
								normals.Add(ns[tri1[i]]);
								indices.Add(indices.Count);
							}
							for (int i = 0; i < 3; i++)
							{
								vertices.Add(vs[tri2[i]]);
								normals.Add(ns[tri2[i]]);
								indices.Add(indices.Count);
							}
						}
					}
				}
			}

			m.vertices = vertices.ToArray();
			m.normals = normals.ToArray();
			m.triangles = indices.ToArray();

			return m;
		}

		public static Vector3 wrap(Vector3 a, ref byte mask, int resolution)
		{
			if (a.x >= resolution) mask |= 1;
			if (a.y >= resolution) mask |= 2;
			if (a.z >= resolution) mask |= 4;

			a.x = (int)a.x % resolution;
			a.y = (int)a.y % resolution;
			a.z = (int)a.z % resolution;

			return a;
		}

		public static int earlyContinues;
		public static int lateContinues;
		public static int latelateContinues;

		public static Mesh GenMesh(int resolution, OctreeDrawInfo[,,] drawInfos, List<Vector3> vertices, List<Vector3> normals)
		{
			List<int> indices = new List<int>();

			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						OctreeDrawInfo drawInfo = drawInfos[x, y, z];
						if (drawInfo == null)
						{
							earlyContinues++;
							continue;
						}
						byte caseCode = (byte)drawInfo.corners;
						Vector3 p = new Vector3(x, y, z);

						/*Debug.Log("P: " + p);

						int v0 = drawInfo.index;
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{
							int ei0 = FarEdges[edgeNum, 0];
							int ei1 = FarEdges[edgeNum, 1];

							//Vector3 offset1 = 

							if (p.x + 1 >= resolution || p.y + 1 >= resolution || p.z + 1 >= resolution)
							{
								latelateContinues++;
								//continue;
							}


							Vector3[] vs = new Vector3[4];
							Vector3[] ns = new Vector3[4];

							Debug.Log("Got here");

							OctreeDrawInfo ogDrawInfo = drawInfo;

							vs[0] = ogDrawInfo.position;
							ns[0] = ogDrawInfo.averageNormal;

							Vector3[][] DCEdgeoffsets = {
								new Vector3[] {new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 1, 1)},
								new Vector3[] {new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 0, 1)},
								new Vector3[] {new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0)}
							};

							bool skip = false;

							Vector3[] DCOffsets = DCEdgeoffsets[edgeNum];
							for (int vertNum = 0; vertNum < 3; vertNum++)
							{
								Debug.Log("VertNum: " + vertNum);
								Debug.Log("P: " + p);
								Vector3 pos = DCOffsets[vertNum] + p;
								Debug.Log("Pos: " + pos);

								if ( (pos.x >= resolution) || 
									 (pos.y >= resolution) || 
									 (pos.z >= resolution) ||
									 (drawInfos[(int)pos.x, (int)pos.y, (int)pos.z] == null)) {
									skip = true;
									continue;
								}


								vs[vertNum + 1] = drawInfos[(int)pos.x, (int)pos.y, (int)pos.z].position;
								ns[vertNum + 1] = drawInfos[(int)pos.x, (int)pos.y, (int)pos.z].averageNormal;
							}

							if(skip) {
								continue;
							}

							int[] tri1 = { 0, 1, 3 };
							int[] tri2 = { 0, 3, 2 };
							if (((ogDrawInfo.corners >> ei0) & 1) == 1 != (edgeNum == 1))
							{ // flip
								tri1[0] = 1; tri1[1] = 0;
								tri2[0] = 3; tri2[1] = 0;
							}

							for (int i = 0; i < 3; i++)
							{
								vertices.Add(vs[tri1[i]]);
								normals.Add(ns[tri1[i]]);
								indices.Add(indices.Count);
							}
							for (int i = 0; i < 3; i++)
							{
								vertices.Add(vs[tri2[i]]);
								normals.Add(ns[tri2[i]]);
								indices.Add(indices.Count);
							}
						}*/

						Vector3Int[][] DCEdgeOffsets = {
							new Vector3Int[] {new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 0), new Vector3Int(0, 1, 1)},
							new Vector3Int[] {new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 0), new Vector3Int(1, 0, 1)},
							new Vector3Int[] {new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0)}
						};

						// DUMB CODE #2 (revised)
						int[] vs = new int[4];
						vs[0] = drawInfo.index;
						//int v0 = drawInfo.index;
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{
							Vector3Int[] ofs = DCEdgeOffsets[edgeNum];
							if (p.x + 1 >= resolution || p.y + 1 >= resolution || p.z + 1 >= resolution)
							{
								latelateContinues++;
								continue;
							}

							int ei0 = FarEdges[edgeNum, 0];
							int ei1 = FarEdges[edgeNum, 1];

							bool edge1 = (caseCode & (1 << ei0)) == (1 << ei0);
							bool edge2 = (caseCode & (1 << ei1)) == (1 << ei1);

							if (edge1 == edge2)
							{
								lateContinues++;
								continue;
							}

							for(int v = 0; v < 3; v++) {
								vs[v + 1] = drawInfos[(int)p.x + ofs[v].x, (int)p.y + ofs[v].y, (int)p.z + ofs[v].z].index;
							}

							int[] t1 = { vs[0], vs[1], vs[3] };
							int[] t2 = { vs[0], vs[3], vs[2] };

							if (((caseCode >> ei0) & 1) == 1 != (edgeNum == 1))
							{ // flip
								t1[0] = vs[1]; t1[1] = vs[0];
								t2[0] = vs[3]; t2[1] = vs[0];
							} 
							for (int i = 0; i < 3; i++)
							{
								indices.Add(t1[i]);
							}
							for(int i = 0; i < 3; i++) {
								indices.Add(t2[i]);
							}
						}


						// DUMB CODE

						/*int v0 = drawInfo.index;
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{
							if (p.x + 1 >= resolution || p.y + 1 >= resolution || p.z + 1 >= resolution)
							{
								latelateContinues++;
								continue;
							}

							int ei0 = FarEdges[edgeNum, 0];
							int ei1 = FarEdges[edgeNum, 1];

							bool edge1 = (caseCode & (1 << ei0)) == (1 << ei0);
							bool edge2 = (caseCode & (1 << ei1)) == (1 << ei1);

							if (edge1 == edge2)
							{
								lateContinues++;
								continue;
							}

							int v1, v2, v3;

							if (edgeNum == 0)
							{
								v1 = drawInfos[(int)p.x, (int)p.y, (int)p.z + 1].index;
								v2 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z].index;
								v3 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z + 1].index;
							}
							else if (edgeNum == 1)
							{
								v1 = drawInfos[(int)p.x, (int)p.y, (int)p.z + 1].index;
								v2 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z].index;
								v3 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z + 1].index;
							}
							else
							{
								v1 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z].index;
								v2 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z].index;
								v3 = drawInfos[(int)p.x + 1, (int)p.y + 1, (int)p.z].index;
							}

							int[] t1 = { v0, v1, v3 };
							int[] t2 = { v0, v3, v2 };

							if (((caseCode >> ei0) & 1) == 1 != (edgeNum == 1))
							{ // flip
								t1[0] = v1; t1[1] = v0;
								t2[0] = v3; t2[1] = v0;
							}
							for (int i = 0; i < 3; i++)
							{
								indices.Add(t1[i]);
							}
							for(int i = 0; i < 3; i++) {
								indices.Add(t2[i]);
							}
						}*/
					}
				}
			}

			//Debug.Log("Early continues: " + earlyContinues);
			//Debug.Log("Late continues: " + lateContinues);
			//Debug.Log("Latelate continues: " + latelateContinues);

			Mesh m = new Mesh();
			m.vertices = vertices.ToArray();
			m.triangles = indices.ToArray();
			m.normals = normals.ToArray();
			return m;

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