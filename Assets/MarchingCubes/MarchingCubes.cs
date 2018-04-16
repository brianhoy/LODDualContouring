using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using UnityEngine;

namespace SE.MC
{	public static class Algorithm
	{
		public static bool FillData(int resolution, Vector3Int start, int stepSize, UtilFuncs.Sampler samp, float[] data) {
			int currentIndex = 0;

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

						float dx = samp(x+f, y, z) - samp(x-f, y, z);
						float dy = samp(x, y+f, z) - samp(x, y-f, z);
						float dz = samp(x, y, z+f) - samp(x, y, z-f);

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

        public static bool PolygonizeArea(int resolution, UtilFuncs.Sampler samp, Chunks.Chunk chunk)
        {
			try {
				int res1 = resolution + 1;
				int resm1 = resolution - 1;
				int resm2 = resolution - 2;

				List<Vector3> vertices = new List<Vector3>();
				List<Vector3> normals = new List<Vector3>();
				List<int> triangles = new List<int>();

				ushort[] edges = new ushort[res1 * res1 * res1 * 3];


				Vector3Int begin = new Vector3Int(0, 0, 0);
				Vector3Int end = new Vector3Int(res1, res1, res1);

				byte lod = (byte)chunk.LODCode;

				System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
				sw.Start();

				if((lod & 1) == 1) begin.x += 1;
				if((lod & 2) == 2) end.x -= 1;
				
				if((lod & 4) == 4) begin.y += 1;
				if((lod & 8) == 8) end.y -= 1;
				
				if((lod & 16) == 16) begin.z += 1;
				if((lod & 32) == 32) end.z -= 1;

				float[] data = new float[res1 * res1 * res1 * 4];

				bool result;

				result = FillData(res1, chunk.Position, (int)Mathf.Pow(2, chunk.LOD), samp, data);

				if(result == false) {
					return false;
				}

				Debug.Log("Got here...");

				CreateVertices(edges, begin, end, vertices, normals, res1, data);


				end -= Vector3Int.one;
				//if((lod & 1) == 1) begin.x += 1;
				Debug.Log("Got here 2...");


				Triangulate(edges, begin, end, triangles, resolution, data);

				//Debug.Log("Phase 1 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
				Debug.Log("Got here 3...");

				sw.Restart();

				//GenerateTransitionCells(vertices, normals, triangles, resolution, data, lod);
				Debug.Log("Got here 4...");

				//Debug.Log("Phase 2 of surface extraction took " + sw.ElapsedMilliseconds + " ms.");
				//MCVT(vertices, triangles, normals, resolution, lod, data);

				chunk.Vertices = vertices;
				chunk.Triangles = triangles.ToArray();
				chunk.Normals = normals;
				return true;
			} catch (System.Exception e) {
				Debug.LogError("Exception: " + e);
				return false;
			}
        }


        public static void CreateVertices(ushort[] edges, Vector3Int begin, Vector3Int end, List<Vector3> vertices, List<Vector3> normals, int res1, float[] data)
        {
			//Debug.Log("CreateVertices called with begin " + begin + ", end: " + end);

            int edgeNum = 0;
            ushort vertNum = 0;
            float density1, density2;

            int res1_3 = res1 * 3;
            int res1_2_3 = res1 * res1 * 3;

			int res1_4 = res1 * 4;
			int res1_2_4 = res1 * res1 * 4;

			int currentIndex = 0;
			int density2index = 0;

            for (int x = begin.x; x < end.x; x++)
            {
                for (int y = begin.y; y < end.y; y++)
                {
                    for (int z = begin.z; z < end.z; z++, edgeNum += 3)
                    {
						currentIndex = GetIndex(x, y, z, res1);
                        density1 = data[currentIndex];

                        if (density1 == 0)
                        {
                            edges[edgeNum] = vertNum;
                            edges[edgeNum + 1] = vertNum;
                            edges[edgeNum + 2] = vertNum;
                            vertNum++;
                            normals.Add(new Vector3(data[currentIndex+1], data[currentIndex+2], data[currentIndex+3]));
                            vertices.Add(new Vector3(x, y, z));
                            continue;
                        }
                        if (y >= begin.y + 1)
                        {
							density2index = GetIndex(x, y-1, z, res1);
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
                                if (density2 == 0)
                                {
                                    edges[edgeNum] = edges[edgeNum - res1_3];
                                }
                                else
                                {
                                    edges[edgeNum] = vertNum;
                                    vertNum++;
                                    normals.Add(LerpN(density1, density2,
                                        data[currentIndex + 1], data[currentIndex + 2], data[currentIndex + 3],
                                        data[density2index + 1], data[density2index + 2], data[density2index + 3]));
                                    vertices.Add(Lerp(density1, density2, x, y, z, x, y - 1, z));
                                }
                            }
                        }
                        if (x >= begin.x + 1)
                        {
							density2index = GetIndex(x-1, y, z, res1);
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
                                if (density2 == 0)
                                {
                                    edges[edgeNum + 1] = edges[edgeNum - res1_2_3];
                                }
                                else
                                {
                                    edges[edgeNum + 1] = vertNum;
                                    vertNum++;
                                    normals.Add(LerpN(density1, density2,
                                        data[currentIndex + 1], data[currentIndex + 2], data[currentIndex + 3],
                                        data[density2index + 1], data[density2index + 2], data[density2index + 3]));
                                    vertices.Add(Lerp(density1, density2, x, y, z, x - 1, y, z));
                                }
                            }
                        }
                        if (z >= begin.z + 1)
                        {
							density2index = GetIndex(x, y, z-1, res1);//currentIndex - res1_2_4;
                            density2 = data[density2index];
                            if ((density1 >= 0 && density2 < 0) || (density2 >= 0 && density1 < 0))
                            {
                                if (density2 == 0)
                                {
                                    edges[edgeNum + 2] = edges[edgeNum - 3];
                                }
                                else
                                {
                                    edges[edgeNum + 2] = vertNum;
                                    vertNum++;
                                    normals.Add(LerpN(density1, density2,
                                        data[currentIndex + 1], data[currentIndex + 2], data[currentIndex + 3],
                                        data[density2index + 1], data[density2index + 2], data[density2index + 3]));
                                    vertices.Add(Lerp(density1, density2, x, y, z, x, y, z - 1));
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void Triangulate(ushort[] edges, Vector3Int begin, Vector3Int end, List<int> triangles, int resolution, float[] data)
        {
            float[] densities = new float[8];
            int mcEdge;

            int res1 = resolution + 1;
            int res1_2 = res1 * res1;
            int t1, t2, t3;

			int res1_4 = res1 * 4;
			int res1_2_4 = res1 * res1 * 4;

			int currentIndex = 0;

            for (int x = begin.x; x < end.x; x++)
            {
                for (int y = begin.y; y < end.y; y++)
                {
                    for (int z = begin.z; z < end.z; z++, currentIndex += 4)
                    {
                        byte caseCode = 0;

                        /*densities[0] = data[currentIndex + res1_2_4];
                        densities[1] = data[currentIndex + res1_2_4 + 4];
                        densities[2] = data[currentIndex + 4];
                        densities[3] = data[currentIndex];
                        densities[4] = data[currentIndex + res1_4 + res1_2_4];
                        densities[5] = data[currentIndex + res1_4 + res1_2_4 + 4];
                        densities[6] = data[currentIndex + res1_4 + 4];
                        densities[7] = data[currentIndex + res1_4];*/

                        densities[0] = data[GetIndex(x, y, z + 1, res1)];
                        densities[1] = data[GetIndex(x + 1, y, z + 1, res1)];
                        densities[2] = data[GetIndex(x, y, z, res1)];
                        densities[3] = data[GetIndex(x, y, z, res1)];
                        densities[4] = data[GetIndex(x, y + 1, z + 1, res1)];
                        densities[5] = data[GetIndex(x + 1, y + 1, z + 1, res1)];
                        densities[6] = data[GetIndex(x + 1, y + 1, z, res1)];
                        densities[7] = data[GetIndex(x, y + 1, z, res1)];

                        if (densities[0] < 0) caseCode |= 1;
                        if (densities[1] < 0) caseCode |= 2;
                        if (densities[2] < 0) caseCode |= 4;
                        if (densities[3] < 0) caseCode |= 8;
                        if (densities[4] < 0) caseCode |= 16;
                        if (densities[5] < 0) caseCode |= 32;
                        if (densities[6] < 0) caseCode |= 64;
                        if (densities[7] < 0) caseCode |= 128;

                        if (caseCode == 0 || caseCode == 255) continue;

                        for (int i = 0; Tables.triTable[caseCode][i] != -1; i += 3)
                        {
                            mcEdge = Tables.triTable[caseCode][i];
                            t1 = edges[3 * (
                                ((x + Tables.MCEdgeToEdgeOffset[mcEdge, 0]) * res1_2) +
                                ((y + Tables.MCEdgeToEdgeOffset[mcEdge, 1]) * res1) +
                                   z + Tables.MCEdgeToEdgeOffset[mcEdge, 2]) +
                                       Tables.MCEdgeToEdgeOffset[mcEdge, 3]];

                            mcEdge = Tables.triTable[caseCode][i + 1];
                            t2 = edges[3 * (
                                ((x + Tables.MCEdgeToEdgeOffset[mcEdge, 0]) * res1_2) +
                                ((y + Tables.MCEdgeToEdgeOffset[mcEdge, 1]) * res1) +
                                   z + Tables.MCEdgeToEdgeOffset[mcEdge, 2]) +
                                       Tables.MCEdgeToEdgeOffset[mcEdge, 3]];

                            mcEdge = Tables.triTable[caseCode][i + 2];
                            t3 = edges[3 * (
                                ((x + Tables.MCEdgeToEdgeOffset[mcEdge, 0]) * res1_2) +
                                ((y + Tables.MCEdgeToEdgeOffset[mcEdge, 1]) * res1) +
                                   z + Tables.MCEdgeToEdgeOffset[mcEdge, 2]) +
                                       Tables.MCEdgeToEdgeOffset[mcEdge, 3]];

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

		public static int GetIndex(int x, int y, int z, int resolution) {
			return 4 * ((z * resolution * resolution) + (y * resolution) + x);
		}

        public static void GenerateTransitionCells(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, int resolution, float[] data, byte lod)
        {
			int resm2 = resolution - 2;
			int res1 = resolution + 1;

            for (int x = 0; x < resolution; x += 2)
            {
                for (int y = 0; y < resolution; y += 2)
                {
                    for (int z = 0; z < resolution; z += 2)
                    {
						bool isMinimal = false;
						if(x == 0 || y == 0 || z == 0) {
							isMinimal = true;
						}
						bool isMaximal = false;
						if(x == resm2 || y == resm2 || z == resm2) {
							isMaximal = true;
						}

						if(!isMinimal && !isMaximal) {
							continue;
						}

                        byte cellLod = 0;

                        if (x == 0) cellLod |= 1;
                        if (x == resolution - 2) cellLod |= 2;
                        if (y == 0) cellLod |= 4;
                        if (y == resolution - 2) cellLod |= 8;
                        if (z == 0) cellLod |= 16;
                        if (z == resolution - 2) cellLod |= 32;

                        cellLod = (byte)(lod & cellLod);

                        byte[][] offsets = Tables.MCLodTable[cellLod];

                        if (offsets.Length > 0)
                        {
                            for (int i = 0; i < offsets.Length; i++)
                            {
								float[] densities = new float[8];
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
									densities[j] = data[GetIndex(pos.x, pos.y, pos.z, res1)];
                                }

								byte caseCode = 0;

								if (densities[0] < 0) caseCode |= 1;
								if (densities[1] < 0) caseCode |= 2;
								if (densities[2] < 0) caseCode |= 4;
								if (densities[3] < 0) caseCode |= 8;
								if (densities[4] < 0) caseCode |= 16;
								if (densities[5] < 0) caseCode |= 32;
								if (densities[6] < 0) caseCode |= 64;
								if (densities[7] < 0) caseCode |= 128;

								if (caseCode == 0 || caseCode == 255) continue;

								int vertCount = vertices.Count;
								int[] vertList = new int[12];

								int bit = 1;
								for(int j = 0; j < 12; j++, bit *= 2) {
									if ((Tables.edgeTable[caseCode] & bit) == bit) {
										int a = Tables.edgePairs[j,0];
										int b = Tables.edgePairs[j,1];
										int indexA = GetIndex(points[a].x, points[a].y, points[a].z, res1);
										int indexB = GetIndex(points[b].x, points[b].y, points[b].z, res1);
										
										normals.Add(LerpN(densities[a], densities[b],
											data[indexA + 1], data[indexA + 2], data[indexA + 3], data[indexB + 1], data[indexB + 2], data[indexB + 3]));
										vertList[j] = vertCount++;
									}
								}

								for (int j = 0; Tables.triTable[caseCode][j] != -1; j++)
								{
									triangles.Add(vertList[Tables.triTable[caseCode][j]]);
								}
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
	}
}