using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using UnityEngine;

namespace SE.MC
{	public static class Algorithm
	{
		private static FastNoiseSIMD noise;

		static Algorithm() {
			noise = new FastNoiseSIMD();
			noise.SetNoiseType(FastNoiseSIMD.NoiseType.Simplex);
			noise.SetFrequency(0.06f);
		}

		public static bool FillData(int resolution, Vector3Int start, int stepSize, UtilFuncs.Sampler samp, float[] data) {
			int currentIndex = 0;

			stepSize /= resolution;

			int endX = resolution * stepSize + start.x;
			int endY = resolution * stepSize + start.y;
			int endZ = resolution * stepSize + start.z;

			float f = 0.001f;

			bool negExists = false;
			bool posExists = false;

			for(int x = start.x; x < endX; x += stepSize) {
				for(int y = start.y; y < endY; y += stepSize) {
					for(int z = start.z; z < endZ; z += stepSize) {
						float density = samp(x,y,z);
						if(density >= 0) {
							posExists = true;
						}
						else {
							negExists = true;
						}

						float dx = density - samp(x-f, y, z);
						float dy = density - samp(x, y-f, z);
						float dz = density - samp(x, y, z-f);

						float total = (dx*dx) + (dy*dy) + (dz*dz);
						total = Mathf.Sqrt(total);

						dx /= total;
						dy /= total;
						dz /= total;

						data[currentIndex] = density;
						data[currentIndex + 1] = dx;
						data[currentIndex + 2] = dy;
						data[currentIndex + 3] = dz;

						currentIndex += 4;
					} 
				}
			}
			return negExists && posExists;
		}

		public static void FillData(int resolution, Chunks.Chunk chunk, float[] data) {
			noise.FillNoiseSet(data, chunk.Position.x, chunk.Position.y, chunk.Position.z, resolution + 2, resolution + 2, resolution + 2);

			/*string noiseStr = "Noise: {";
			for(int i = 0; i < resolution * resolution; i++) {
				noiseStr += data[i] + ", ";
			}
			noiseStr += "}";
			Debug.Log(noiseStr);*/

		}

		public static void GenModifiedTriTable(int res1) {
			int res1_2 = res1 * res1;
			string str = "public readonly static int[][] mTriTable  = new int[][] {\n";

			int[][] modifiedTriTable = new int[Tables.triTable.Length][];

			for(int i = 0; i < Tables.triTable.Length; i++) {
				int[] tris = Tables.triTable[i];
				modifiedTriTable[i] = new int[tris.Length];
				str += "	new int[] {";
				for(int j = 0; j < tris.Length; j++) {
					int tri = tris[j];

					if(tri != -1) {
						tri = GetEdgeIndex(
							Tables.MCEdgeToEdgeOffset[tri, 0], 
							Tables.MCEdgeToEdgeOffset[tri, 1], 
							Tables.MCEdgeToEdgeOffset[tri, 2], 
							Tables.MCEdgeToEdgeOffset[tri, 3], res1);
						/*(3 * ((Tables.MCEdgeToEdgeOffset[tri, 1] * res1 * res1) + 
							(Tables.MCEdgeToEdgeOffset[tri, 0] * res1) + 
							Tables.MCEdgeToEdgeOffset[tri, 2])) + 
							Tables.MCEdgeToEdgeOffset[tri, 3]; */
						/*3 * (Tables.MCEdgeToEdgeOffset[tri, 1] * res1_2 +
							Tables.MCEdgeToEdgeOffset[tri, 0] * res1 +
							Tables.MCEdgeToEdgeOffset[tri, 2]) +
							Tables.MCEdgeToEdgeOffset[tri, 3];*/
					}

					modifiedTriTable[i][j] = tri;

					str += tri;
					if(j < tris.Length - 1) {
						str += ", ";
					}
				}
				str += "}, \n";
				Tables.m2TriTable = modifiedTriTable;
			}
			str += "}";
#if UNITY_EDITOR
			UnityEditor.EditorGUIUtility.systemCopyBuffer = str;
#endif
			Debug.Log("Modified Triangle Table: \n" + str);
		}
		public static bool PolygonizeArea(int resolution, UtilFuncs.Sampler samp, Chunks.Chunk chunk) {
			int res2 = resolution + 2;
			float[] data = new float[res2*res2*res2];

			return PolygonizeArea(resolution, samp, chunk, data);
		}

        public static bool PolygonizeArea(int resolution, UtilFuncs.Sampler samp, Chunks.Chunk chunk, float[] data)
        {
			GenModifiedTriTable(resolution + 1);

			double fillDataMs, createVerticesMs, triangulateMs, transitionCellMs = 0;
			int res1 = resolution + 1;

			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch(); sw.Start();
			FillData(resolution, chunk, data);
			sw.Stop(); fillDataMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			int resm1 = resolution - 1;
			int resm2 = resolution - 2;

			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			List<int> triangles = new List<int>();

			ushort[] edges = new ushort[res1 * res1 * res1 * 3];

			Vector3Int begin = new Vector3Int(0, 0, 0);
			Vector3Int end = new Vector3Int(res1, res1, res1);

			byte lod = (byte)chunk.LODCode;

			if((lod & 1) == 1) begin.x += 1;
			if((lod & 2) == 2) end.x -= 1;
			
			if((lod & 4) == 4) begin.y += 1;
			if((lod & 8) == 8) end.y -= 1;
			
			if((lod & 16) == 16) begin.z += 1;
			if((lod & 32) == 32) end.z -= 1;



			CreateVertices(edges, begin, end, vertices, normals, res1, data);
			sw.Stop(); createVerticesMs = sw.Elapsed.TotalMilliseconds; sw.Restart();
			end -= Vector3Int.one;

			Triangulate(edges, begin, end, triangles, resolution, data);
			sw.Stop(); triangulateMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			//Debug.Log("Phase 1 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
			if(lod != 0) {
				GenerateTransitionCells(vertices, normals, triangles, resolution, data, lod);
			}
			sw.Stop(); transitionCellMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			//Debug.Log("Phase 2 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
			//MCVT(vertices, triangles, normals, resolution, lod, data);

			Debug.Log("Done meshing " + resolution + "^3 chunk. " + "FillData ms: " + fillDataMs + ", CreateVertices ms: " + createVerticesMs + ", Triangulate ms: " + triangulateMs + ", TransitionCell ms: " + transitionCellMs + " (Total: " + (fillDataMs + createVerticesMs + triangulateMs + transitionCellMs) + "ms. )");

			chunk.Vertices = vertices;
			chunk.Triangles = triangles.ToArray();
			chunk.Normals = normals;
			return true;
        }

        public static bool PolygonizeAreaDecked(int resolution, Chunks.Chunk chunk, float[] data)
        {
			GenModifiedTriTable(resolution + 1);

			double fillDataMs, createVerticesMs, triangulateMs, transitionCellMs = 0;
			int res1 = resolution + 1;

			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch(); sw.Start();
			FillData(resolution, chunk, data);
			sw.Stop(); fillDataMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			int resm1 = resolution - 1;
			int resm2 = resolution - 2;

			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			List<int> triangles = new List<int>();

			ushort[] edges = new ushort[res1 * res1 * res1 * 3];

			Vector3Int begin = new Vector3Int(0, 0, 0);
			Vector3Int end = new Vector3Int(res1, res1, res1);

			byte lod = (byte)chunk.LODCode;

			if((lod & 1) == 1) begin.x += 1;
			if((lod & 2) == 2) end.x -= 1;
			
			if((lod & 4) == 4) begin.y += 1;
			if((lod & 8) == 8) end.y -= 1;
			
			if((lod & 16) == 16) begin.z += 1;
			if((lod & 32) == 32) end.z -= 1;


			DeckMC(begin, end, vertices, normals, triangles, resolution, data);

			sw.Stop(); createVerticesMs = sw.Elapsed.TotalMilliseconds; sw.Restart();
			/*if(lod != 0) {
				GenerateTransitionCells(vertices, normals, triangles, resolution, data, lod);
			}*/
			sw.Stop(); transitionCellMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			//Debug.Log("Phase 2 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
			//MCVT(vertices, triangles, normals, resolution, lod, data);

			Debug.Log("Done meshing decked " + resolution + "^3 chunk. " + "FillData ms: " + fillDataMs + ", CreateVertices ms: " + createVerticesMs + ", TransitionCell ms: " + transitionCellMs + " (Total: " + (fillDataMs + createVerticesMs + transitionCellMs) + "ms. )");

			chunk.Vertices = vertices;
			chunk.Triangles = triangles.ToArray();
			chunk.Normals = normals;
			return true;
        }


		public class BenchmarkResult {
			public double fillDataMs;
			public double createVerticesMs;
			public double triangulateMs;
			public double transitionCellMs;
		}

		public static BenchmarkResult PolygonizeAreaBenchmarked(int resolution, UtilFuncs.Sampler samp, Chunks.Chunk chunk, float[] data)
        {
			BenchmarkResult result = new BenchmarkResult();
			double fillDataMs, createVerticesMs, triangulateMs, transitionCellMs = 0;
			int res1 = resolution + 1;

			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch(); sw.Start();
			FillData(resolution, chunk, data);
			sw.Stop(); fillDataMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			int resm1 = resolution - 1;
			int resm2 = resolution - 2;

			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			List<int> triangles = new List<int>();

			ushort[] edges = new ushort[res1 * res1 * res1 * 3];

			Vector3Int begin = new Vector3Int(0, 0, 0);
			Vector3Int end = new Vector3Int(res1, res1, res1);

			byte lod = (byte)chunk.LODCode;

			if((lod & 1) == 1) begin.x += 1;
			if((lod & 2) == 2) end.x -= 1;
			
			if((lod & 4) == 4) begin.y += 1;
			if((lod & 8) == 8) end.y -= 1;
			
			if((lod & 16) == 16) begin.z += 1;
			if((lod & 32) == 32) end.z -= 1;



			CreateVertices(edges, begin, end, vertices, normals, res1, data);
			sw.Stop(); createVerticesMs = sw.Elapsed.TotalMilliseconds; sw.Restart();
			end -= Vector3Int.one;

			Triangulate(edges, begin, end, triangles, resolution, data);
			sw.Stop(); triangulateMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			//Debug.Log("Phase 1 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
			if(lod != 0) {
				GenerateTransitionCells(vertices, normals, triangles, resolution, data, lod);
			}
			sw.Stop(); transitionCellMs = sw.Elapsed.TotalMilliseconds; sw.Restart();

			//Debug.Log("Phase 2 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
			//MCVT(vertices, triangles, normals, resolution, lod, data);

			result.createVerticesMs = createVerticesMs;
			result.fillDataMs = fillDataMs;
			result.transitionCellMs = transitionCellMs;
			result.triangulateMs = triangulateMs;

			chunk.Vertices = vertices;
			chunk.Triangles = triangles.ToArray();
			chunk.Normals = normals;
			return result;
        }

        public static void CreateVertices(ushort[] edges, Vector3Int begin, Vector3Int end, List<Vector3> vertices, List<Vector3> normals, int res1, float[] data)
        {
            int edgeNum = 0;
            ushort vertNum = 0;
            float density1, density2;
			int res2 = res1 + 1;

            int res1_3 = res1 * 3;
            int res1_2_3 = res1 * res1 * 3;

			int res1_4 = res1 * 4;
			int res1_2_4 = res1 * res1 * 4;

			int res2_2 = res2 * res2;

			int currentIndex = 0;
			int density2index = 0;

			int beginx = begin.x;
			int beginy = begin.y;
			int beginz = begin.z;

			int endx = end.x;
			int endy = end.y;
			int endz = end.z;

			float density, total;
			float n1x, n1y, n1z, n2x, n2y, n2z, mu;

            for (int x = beginx; x < endx; x++)
            {
                for (int y = beginy; y < endy; y++)
                {
                    for (int z = beginz; z < endz; z++)
                    {
						currentIndex = (x * res2 * res2) + (y * res2) + z;
						edgeNum = 3 * ((x * res1 * res1) + (y * res1) + z);
                        density1 = data[currentIndex];

						density = data[currentIndex];
						n1x = data[currentIndex + res2_2] - density;
						n1y = data[currentIndex + res2] - density;
						n1z = data[currentIndex + 1] - density;

						total = (n1x*n1x) + (n1y*n1y) + (n1z*n1z);
						total = Mathf.Sqrt(total);

						n1x /= total;
						n1y /= total;
						n1z /= total;

                        if (y > beginy)
                        {
							density2index = currentIndex - res2;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;

								mu = density1 / (density1 - density2);

								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x, y - mu, z));
                            }
                        }
                        if (x > beginx)
                        {
							density2index = currentIndex - res2_2;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum + 1] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;

								mu = density1 / (density1 - density2);
								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x - mu, y, z));
                            }
                        }
                        if (z > beginz)
                        {
							density2index = currentIndex - 1;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum + 2] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;
								
								mu = density1 / (density1 - density2);
								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x, y, z - mu));
                            }
                        }
                    }
                }
            }
        }
        public static void Triangulate(ushort[] edges, Vector3Int begin, Vector3Int end, List<int> triangles, int resolution, float[] data)
        {
            int mcEdge;

            int res1 = resolution + 1;
            int res1_2 = res1 * res1;
            int t1, t2, t3;

			int res1_4 = res1 * 4;
			int res1_2_4 = res1 * res1 * 4;

			int res2 = resolution + 2;
			int res2_2 = res2 * res2;

			int currentIndex = 0;

			int beginx = begin.x;
			int beginy = begin.y;
			int beginz = begin.z;

			int endx = end.x;
			int endy = end.y;
			int endz = end.z;

            for (int x = beginx; x < endx; x++)
            {
                for (int y = beginy; y < endy; y++)
                {
                    for (int z = beginz; z < endz; z++)
                    {
						currentIndex = ((x * res2 * res2) + (y * res2) + z);
                        byte caseCode = 0;

                        if (data[currentIndex + 1] >= 0) caseCode |= 1;
                        if (data[currentIndex + res2_2 + 1] >= 0) caseCode |= 2;
                        if (data[currentIndex + res2_2] >= 0) caseCode |= 4;
                        if (data[currentIndex] >= 0) caseCode |= 8;
                        if (data[currentIndex + res2 + 1] >= 0) caseCode |= 16;
                        if (data[currentIndex + res2_2 + res2 + 1] >= 0) caseCode |= 32;
                        if (data[currentIndex + res2_2 + res2] >= 0) caseCode |= 64;
                        if (data[currentIndex + res2] >= 0) caseCode |= 128;



                        /*if (data[currentIndex + res2_2] >= 0) caseCode |= 1;
                        if (data[currentIndex + res2_2 + 1] >= 0) caseCode |= 2;
                        if (data[currentIndex + 1] >= 0) caseCode |= 4;
                        if (data[currentIndex] >= 0) caseCode |= 8;
                        if (data[currentIndex + res2 + res2_2] >= 0) caseCode |= 16;
                        if (data[currentIndex + res2 + res2_2 + 1] >= 0) caseCode |= 32;
                        if (data[currentIndex + res2 + 1] >= 0) caseCode |= 64;
                        if (data[currentIndex + res2] >= 0) caseCode |= 128;*/



                        if (caseCode == 0 || caseCode == 255) continue;

						int currentEdge = 3 * (res1_2 * x + res1 * y + z);

                        for (int i = 0; Tables.triTable[caseCode][i] != -1; i += 3)
                        {
                            mcEdge = Tables.triTable[caseCode][i];

							t1 = edges[currentEdge + Tables.mTriTable[caseCode][i]];
							t2 = edges[currentEdge + Tables.mTriTable[caseCode][i+1]];
							t3 = edges[currentEdge + Tables.mTriTable[caseCode][i+2]];


                            if (t1 != t2 && t2 != t3 && t1 != t3)
                            {
                                triangles.Add(t1);
                                triangles.Add(t2);
                                triangles.Add(t3);
                            }
                        }

                    }
                }
            }
        }

		public static void DeckMC(Vector3Int begin, Vector3Int end, List<Vector3> vertices, List<Vector3> normals, List<int> triangles, int resolution, float[] data) {
            int edgeNum = 0;
            ushort vertNum = 0;
            float density1, density2;
			int res1 = resolution + 1;
            int res1_2 = res1 * res1;

            int res1_3 = res1 * 3;
            int res1_2_3 = res1 * res1 * 3;

			int res1_4 = res1 * 4;
			int res1_2_4 = res1 * res1 * 4;

			int res2 = res1 + 1;
			int res2_2 = res2 * res2;

			int currentIndex = 0;
			int density2index = 0;

			int beginx = begin.x;
			int beginy = begin.y;
			int beginz = begin.z;

			int endx = end.x;
			int endy = end.y;
			int endz = end.z;

			float total;
			float n1x, n1y, n1z, n2x, n2y, n2z, mu;

			int edgeArraySize = res1 * res1 * 2 * 3;
			ushort[] edges = new ushort[edgeArraySize];
			int currentDeck = 0;
			ushort t1, t2, t3;

			for (int y = beginy, ty = beginy - 1; y < endy; y++, ty++)
			{
				// Generate vertices for a deck
				for (int x = beginx; x < endx; x++)
				{
                    for (int z = beginz; z < endz; z++)
                    {
						currentIndex = (x * res2 * res2) + (y * res2) + z;
						edgeNum = GetEdgeIndex(x, y, z, 0, res1); //3 * ((currentDeck * res1 * res1) + (x * res1) + z);
                        density1 = data[currentIndex];

						n1x = data[currentIndex + res2_2] - density1;
						n1y = data[currentIndex + res2] - density1;
						n1z = data[currentIndex + 1] - density1;

						total = (n1x*n1x) + (n1y*n1y) + (n1z*n1z);
						total = Mathf.Sqrt(total);

						n1x /= total;
						n1y /= total;
						n1z /= total;

                        if (y > beginy)
                        {
							density2index = currentIndex - res2;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;

								mu = density1 / (density1 - density2);

								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x, y - mu, z));
                            }
                        }
                        if (x > beginx)
                        {
							density2index = currentIndex - res2_2;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum + 1] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;

								mu = density1 / (density1 - density2);
								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x - mu, y, z));
                            }
                        }
                        if (z > beginz)
                        {
							density2index = currentIndex - 1;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
								edges[edgeNum + 2] = vertNum++;
								n2x = data[density2index + res2_2] - density2;
								n2y = data[density2index + res2] - density2;
								n2z = data[density2index + 1] - density2;

								total = (n2x*n2x) + (n2y*n2y) + (n2z*n2z);
								total = Mathf.Sqrt(total);

								n2x /= total;
								n2y /= total;
								n2z /= total;
								
								mu = density1 / (density1 - density2);
								normals.Add(new Vector3(n1x + mu * (n2x - n1x), n1y + mu * (n2y - n1y), n1z + mu * (n2z - n1z)));
								vertices.Add(new Vector3(x, y, z - mu));
                            }
                        }
                    }
                }

				// Generate indices for a deck
				if(ty == beginy - 1 || ty >= endy) continue;

                for (int x = beginx; x < endx - 1; x++)
                {
                    for (int z = beginz; z < endz - 1; z++)
                    {
						currentIndex = ((x * res2 * res2) + (ty * res2) + z);
                        byte caseCode = 0;

                        if (data[currentIndex + 1] >= 0) caseCode |= 1;
                        if (data[currentIndex + res2_2 + 1] >= 0) caseCode |= 2;
                        if (data[currentIndex + res2_2] >= 0) caseCode |= 4;
                        if (data[currentIndex] >= 0) caseCode |= 8;
                        if (data[currentIndex + res2 + 1] >= 0) caseCode |= 16;
                        if (data[currentIndex + res2_2 + res2 + 1] >= 0) caseCode |= 32;
                        if (data[currentIndex + res2_2 + res2] >= 0) caseCode |= 64;
                        if (data[currentIndex + res2] >= 0) caseCode |= 128;

                        if (caseCode == 0 || caseCode == 255) continue;

						int currentEdge = GetEdgeIndex(x, ty, z, 0, res1); //3 * (res1_2 * ty%2 + res1 * x + z);

                        for (int i = 0; Tables.triTable[caseCode][i] != -1; i += 3)
                        {
							int mcEdge;
							/*int mcEdge = Tables.triTable[caseCode][i]; 
							
							//t1 = 3 * 

							//Tables.MCEdgeToEdgeOffset[i];*/
							int index2 = currentEdge + Tables.m2TriTable[caseCode][i];

							index2 = ((index2)) % edgeArraySize;

							/*if(index2 > edgeArraySize) {
								index2 -= res1_2;
							}*/

                            mcEdge = Tables.triTable[caseCode][i];
							int index = GetEdgeIndex(
								x + Tables.MCEdgeToEdgeOffset[mcEdge, 0], 
								ty + Tables.MCEdgeToEdgeOffset[mcEdge, 1], 
								z + Tables.MCEdgeToEdgeOffset[mcEdge, 2], 
								Tables.MCEdgeToEdgeOffset[mcEdge, 3], res1);

							if(index2 > edgeArraySize) {
								Debug.Log("Index greater than edge array size. EdgeArray size: " + edgeArraySize + ", Index2: " + index2 + ", TrueIndex: " + index + ", Lookup Table value for case " + caseCode + " tri " + i + ": " + Tables.m2TriTable[caseCode][i] + ", res1_2: " + res1_2);
							}

							t1 = edges[(currentEdge + Tables.m2TriTable[caseCode][i]) % edgeArraySize];
							//t2 = edges[(currentEdge + Tables.m2TriTable[caseCode][i+1]) % edgeArraySize];
							//t3 = edges[(currentEdge + Tables.m2TriTable[caseCode][i+2]) % edgeArraySize];

                           // t1 = edges[index];

                            mcEdge = Tables.triTable[caseCode][i + 1];
							index = GetEdgeIndex(
								x + Tables.MCEdgeToEdgeOffset[mcEdge, 0], 
								ty + Tables.MCEdgeToEdgeOffset[mcEdge, 1], 
								z + Tables.MCEdgeToEdgeOffset[mcEdge, 2], 
								Tables.MCEdgeToEdgeOffset[mcEdge, 3], res1);
                            t2 = edges[index];

                            mcEdge = Tables.triTable[caseCode][i + 2];
							index = GetEdgeIndex(
								x + Tables.MCEdgeToEdgeOffset[mcEdge, 0], 
								ty + Tables.MCEdgeToEdgeOffset[mcEdge, 1], 
								z + Tables.MCEdgeToEdgeOffset[mcEdge, 2], 
								Tables.MCEdgeToEdgeOffset[mcEdge, 3], res1);
                            t3 = edges[index];


                            if (t1 != t2 && t2 != t3 && t1 != t3)
                            {
                                triangles.Add(t1);
                                triangles.Add(t2);
                                triangles.Add(t3);
                            }
                        }

                    }
				}

				currentDeck = (currentDeck + 1) % 2;
            }

		}

		public static int GetEdgeIndex(int x, int y, int z, int edgeNum, int res1) {
			return 3 * (( (y%2) * res1 * res1) + (x * res1) + z) + edgeNum;
		}

        public static void GenerateTransitionCells(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, int resolution, float[] data, byte lod)
        {
			int resm2 = resolution - 2;
			int res1 = resolution + 1;
			int res2 = resolution + 2;

			int cellMask = 0;
			int cellLod = 0;
			byte[][] offsets;
			float[] densities = new float[8];
			float[] pxs = new float[8];
			float[] pys = new float[8];
			float[] pzs = new float[8];

            for (int x = 0; x < resolution; x += 2)
            {
				if (x == 0) { cellMask |= 1; } else { cellMask &= ~1; }
				if (x == resolution - 2) { cellMask |= 2; } else { cellMask &= ~2; }

                for (int y = 0; y < resolution; y += 2)
                {
					if (y == 0) { cellMask |= 4; } else { cellMask &= ~4; }
					if (y == resolution - 2) { cellMask |= 8; } else { cellMask &= ~8; }

                    for (int z = 0; z < resolution; z += 2)
                    {
                        if (z == 0) { cellMask |= 16; } else { cellMask &= ~16; }
                        if (z == resolution - 2) { cellMask |= 32; } else { cellMask &= ~32; }
                        
						cellLod = (byte)(lod & cellMask);

						if(cellLod == 0) {
							continue;
						}

                        offsets = Tables.MCLodTable[cellLod];

                        if (offsets.Length > 0)
                        {
                            for (int i = 0; i < offsets.Length; i++)
                            {
								Vector3Int[] points = new Vector3Int[8];

                                for (int j = 0; j < 8; j++)
                                {
                                    Vector3Int pos = new Vector3Int(x + 1, y + 1, z + 1);
                                    byte offset = offsets[i][j];
                                    if ((offset & 1) == 1) pos.x -= 1;
                                    if ((offset & 2) == 2) pos.x += 1;
                                    if ((offset & 4) == 4) pos.y -= 1;
                                    if ((offset & 8) == 8) pos.y += 1;
                                    if ((offset & 16) == 16) pos.z -= 1;
                                    if ((offset & 32) == 32) pos.z += 1;

									points[j] = pos;
									densities[j] = data[GetIndex2(pos.x, pos.y, pos.z, res2)];
                                }

								byte caseCode = 0;

								if (densities[0] >= 0) caseCode |= 1;
								if (densities[1] >= 0) caseCode |= 2;
								if (densities[2] >= 0) caseCode |= 4;
								if (densities[3] >= 0) caseCode |= 8;
								if (densities[4] >= 0) caseCode |= 16;
								if (densities[5] >= 0) caseCode |= 32;
								if (densities[6] >= 0) caseCode |= 64;
								if (densities[7] >= 0) caseCode |= 128;

								if (caseCode == 0 || caseCode == 255) continue;

								//caseCode = (byte)~caseCode;

								int vertCount = vertices.Count;
								int[] vertList = new int[12];

								int bit = 1;
								for(int j = 0; j < 12; j++, bit *= 2) {
									if ((Tables.edgeTable[caseCode] & bit) == bit) {
										int a = Tables.edgePairs[j,0];
										int b = Tables.edgePairs[j,1];
										int indexA = GetIndex(points[a].x, points[a].y, points[a].z, res1);
										int indexB = GetIndex(points[b].x, points[b].y, points[b].z, res1);

										Vector3 na = GetNormal(points[a].x, points[a].y, points[a].z, res2, data);
										Vector3 nb = GetNormal(points[b].x, points[b].y, points[b].z, res2, data);

										vertices.Add(Lerp2(densities[a], densities[b], points[a], points[b]));



										normals.Add(Lerp2(densities[a], densities[b], na, nb));
										vertList[j] = vertCount++;
									}
								}

								for (int j = 0; Tables.triTable[caseCode][j] != -1; j++)
								{
									/*int v = j;
									if(j % 3 == 0) {v = j + 1; }
									if(j % 3 == 1) {v = j - 1; } */

									triangles.Add(vertList[Tables.triTable[caseCode][j]]);
								}
                            }
                        }
                    }
                }
            }
        }

		public static int GetIndex(int x, int y, int z, int resolution) {
			return 4 * ((z * resolution * resolution) + (y * resolution) + x);
		}

        public static int GetEdge3D(int x, int y, int z, int edgeNum, int res)
        {
            return (3 * ((z * res * res) + (y * res) + x)) + edgeNum;
        }
        public static int GetEdge3DB(int x, int y, int z, int edgeNum, int res)
        {
            return (3 * ((x * res * res) + (y * res) + z)) + edgeNum;
        }
		public static int GetIndex2(int x, int y, int z, int res2) {
			return (x * res2 * res2) + (y * res2) + z;	
		}

		public static Vector3 GetNormal(int x, int y, int z, int res2, float[] data) {
			int index = GetIndex2(x, y, z, res2);

			float density = data[index];

			float dx = data[GetIndex2(x+1, y, z, res2)] - density;
			float dy = data[GetIndex2(x, y+1, z, res2)] - density;
			float dz = data[GetIndex2(x, y, z+1, res2)] - density;

			float total = (dx*dx) + (dy*dy) + (dz*dz);
			total = Mathf.Sqrt(total);

			dx /= total;
			dy /= total;
			dz /= total;

			return new Vector3(dx, dy, dz);
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

			float mu = (density1) / (density1 - density2);

			return new Vector3(x1 + mu * (x2 - x1), y1 + mu * (y2 - y1), z1 + mu * (z2 - z1));
		}
		public static Vector3 LerpN(float density1, float density2, float n1x, float n1y, float n1z, float n2x, float n2y, float n2z)
		{
			float mu = Mathf.Round((density1) / (density1 - density2) * 256f) / 256f;

			return new Vector3(n1x / 127f + mu * (n2x / 127f - n1x / 127f), n1y / 127f + mu * (n2y / 127f - n1y / 127f), n1z / 127f + mu * (n2z / 127f - n1z / 127f));
		}

        public static Vector3 Lerp2(float density1, float density2, Vector3 A, Vector3 B)
        {
            if (density1 < 0.00001f && density1 > -0.00001f)
            {
                return new Vector3(A.x, A.y, A.z);
            }
            if (density2 < 0.00001f && density2 > -0.00001f)
            {
                return new Vector3(B.x, B.y, B.z);
            }
            /*if(Mathf.Abs(density1 - density2) < 0.00001f) {
                return new Vector3(x2, y2, z2);
            }*/

            float mu = Mathf.Round((density1) / (density1 - density2) * 256) / 256.0f;

            return new Vector3(A.x + mu * (B.x - A.x), A.y + mu * (B.y - A.y), A.z + mu * (B.z - A.z));
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

		public static void DrawGizmos() {
		}
	}
}