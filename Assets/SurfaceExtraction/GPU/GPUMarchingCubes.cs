using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chunks;

namespace Chunks {
public struct GPUMCJob {
    public Vector3 Position;
    public float Scale;
}

public class GPUChunk {
	public ComputeBuffer AppendVertexBuffer;
	public ComputeBuffer ArgBuffer;
	public Material Material;
	public Matrix4x4 TransformationMatrix;
	public Vector3 Position;
	public Vector3 Scale;

	public UnityEngine.Experimental.Rendering.AsyncGPUReadbackRequest ArgBufferRequest;
	public bool Done;
	public int VertCount;
}
}

public class GPUMarchingCubes : MonoBehaviour {
    public ComputeShader MarchingCubesCS; 
	public ComputeShader NoiseCS;

    private Queue<GPUMCJob> Jobs;
    public GPUChunk[] Chunks;

	private int MarchingCubesKernel;
	private int NoiseKernel;
	private int MaxVertices;
	public Material Mat;
	private RenderTexture DensityTexture;
	private Texture3D TestTexture;
	public GameObject TestQuad;

	public List<GPUChunk> OutstandingChunks; // CPU still getting vertcount for these chunks...

    public int Resolution;
    private int CurrentIndex;

	private UnityEngine.Rendering.CommandBuffer GenerateCommandBuffer;
	private UnityEngine.Rendering.CommandBuffer RenderCommandBuffer;

	private bool NewChunks;
	private int ChunkCount;

	public void Start() {
		this.NewChunks = false;
		this.MaxVertices = (Resolution - 1) * (Resolution - 1) * (Resolution - 1) * 5;
		this.GenerateCommandBuffer = new UnityEngine.Rendering.CommandBuffer();
		this.RenderCommandBuffer = new UnityEngine.Rendering.CommandBuffer();
		this.CurrentIndex = 0;
		this.ChunkCount = 100;
		this.OutstandingChunks = new List<GPUChunk>();

		InitializeChunks();
		IntializeDensityTexture();
		IntializeComputeShader();
		InitializeTestTexture();

		AddTestChunks();
	}

	private void AddTestChunks() {
		for(int i = 0; i < 5; i++) {
			GPUMCJob job = new GPUMCJob();
			job.Position = new Vector3(i, 0, 0);
			job.Scale = 128f/126f;
			AddChunk(job);
		}
	}

	private void InitializeChunks() {
		this.Chunks = new GPUChunk[ChunkCount];
		for(int i = 0; i < ChunkCount; i++) {
			Chunks[i] = new GPUChunk();
			Chunks[i].AppendVertexBuffer = new ComputeBuffer(MaxVertices, sizeof(float) * 18, ComputeBufferType.Append);
			Chunks[i].Material = UnityEngine.Object.Instantiate(this.Mat);
			Chunks[i].VertCount = MaxVertices;
			Chunks[i].TransformationMatrix = Matrix4x4.identity;
			Chunks[i].ArgBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
			Chunks[i].ArgBuffer.SetData(new int[] { MaxVertices, 1, 0, 0 });
		}
	}

	private void IntializeDensityTexture() {
		RenderTextureDescriptor rtd = new RenderTextureDescriptor();
		rtd.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		rtd.width = Resolution;
		rtd.height = Resolution;
		rtd.volumeDepth = Resolution;
		rtd.msaaSamples = 4;
		rtd.enableRandomWrite = true;
		rtd.colorFormat = RenderTextureFormat.RFloat;
		rtd.autoGenerateMips = false;

		DensityTexture = new RenderTexture(rtd);
		DensityTexture.wrapMode = TextureWrapMode.Mirror;
		DensityTexture.Create();

		TestQuad.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", DensityTexture);
	}
	
	private void IntializeComputeShader() {
		this.MarchingCubesKernel = MarchingCubesCS.FindKernel("MarchingCubes");
		this.NoiseKernel = NoiseCS.FindKernel("CSMain");

		//MarchingCubesCS.SetTexture(MarchingCubesKernel, "_densityTexture", DensityTexture);
		MarchingCubesCS.SetInt("_gridSize", Resolution);
		MarchingCubesCS.SetFloat("_isoLevel", 0);
	}

	private void InitializeTestTexture() {
		TestTexture = new Texture3D(Resolution, Resolution, Resolution, TextureFormat.RFloat, false);
		TestTexture.wrapMode = TextureWrapMode.Clamp;

		Color[] colors = new Color[Resolution * Resolution * Resolution];

		var idx = 0;
		float sx, sy, sz;
		float resol = (Resolution - 2) / 2 * Mathf.Sin(0.634f);
		for (var z = 0; z < Resolution; ++z)
		{
			for (var y = 0; y < Resolution; ++y)
			{
				for (var x = 0; x < Resolution; ++x, ++idx)
				{
					sx = x - Resolution / 2;
					sy = y - Resolution / 2;
					sz = z - Resolution / 2;
					var amount = (sx * sx + sy * sy + sz * sz) <= resol * resol ? 1 : 0;
					colors[idx].r = amount;
				}
			}
		}
		TestTexture.SetPixels(colors);
		TestTexture.Apply();

		MarchingCubesCS.SetTexture(MarchingCubesKernel, "_densityTexture", TestTexture);
	}

	private void IntializeRenderCommandBuffer() {
		RenderCommandBuffer.DrawProcedural(Matrix4x4.identity, Mat, 0, MeshTopology.Triangles, MaxVertices);
	}

	public void OnRenderObject()
	{
		//Debug.Log("OnRenderObject called");
		RenderCommandBuffer.Clear();
		for(int i = 0; i < CurrentIndex; i++) {
			GPUChunk c = Chunks[i];
			Material mat = c.Material;
			mat.SetPass(0);
			mat.SetBuffer("triangles", c.AppendVertexBuffer);
			mat.SetMatrix("model", c.TransformationMatrix); 

			//Graphics.DrawProceduralIndirect(MeshTopology.Triangles, c.ArgBuffer, 0);
			RenderCommandBuffer.DrawProceduralIndirect(c.TransformationMatrix, mat, 0, MeshTopology.Triangles, c.ArgBuffer);
			//RenderCommandBuffer.DrawProcedural(c.TransformationMatrix, c.Material, 0, MeshTopology.Triangles, c.VertCount);
		}
		Graphics.ExecuteCommandBuffer(RenderCommandBuffer);
	}

    public void Update() {
        Graphics.ExecuteCommandBuffer(GenerateCommandBuffer);
		GenerateCommandBuffer.Clear();
    }

    public void AddChunk(GPUMCJob job) {
		Debug.Log("Adding chunk...");

		GPUChunk c = Chunks[CurrentIndex++];
		c.Done = false;
		c.Scale = job.Scale * Vector3.one;
		c.Position = job.Position;
		c.TransformationMatrix = c.TransformationMatrix * Matrix4x4.Translate(job.Position) * Matrix4x4.Scale(Vector3.one * job.Scale);

		c.AppendVertexBuffer.SetCounterValue(0);
		//NoiseCS.SetMatrix("objtransform", );


		GenerateCommandBuffer.SetComputeVectorParam(NoiseCS, "_scale", c.Scale);
		GenerateCommandBuffer.SetComputeVectorParam(NoiseCS, "_offset", c.Position);
		GenerateCommandBuffer.SetComputeMatrixParam(NoiseCS, "_localToWorld", c.TransformationMatrix);
		GenerateCommandBuffer.SetComputeTextureParam(NoiseCS, NoiseKernel, "Result", DensityTexture);
		GenerateCommandBuffer.DispatchCompute(NoiseCS, NoiseKernel, Resolution/4, Resolution/4, Resolution/4);
		Graphics.ExecuteCommandBuffer(GenerateCommandBuffer); // UnityEngine.Rendering.ComputeQueueType.Default

		GenerateCommandBuffer.SetComputeBufferParam(MarchingCubesCS, MarchingCubesKernel, "triangleRW", c.AppendVertexBuffer);
		GenerateCommandBuffer.SetComputeTextureParam(MarchingCubesCS, MarchingCubesKernel, "_densityTexture", DensityTexture);
		GenerateCommandBuffer.DispatchCompute(MarchingCubesCS, MarchingCubesKernel, Resolution / 8, Resolution / 8, Resolution / 8);
		GenerateCommandBuffer.CopyCounterValue(c.AppendVertexBuffer, c.ArgBuffer, 0);
		Graphics.ExecuteCommandBuffer(GenerateCommandBuffer); // UnityEngine.Rendering.ComputeQueueType.Default */
		GenerateCommandBuffer.Clear();

		int[] args = new int[] { 2, 1, 0, 0 };

		c.ArgBuffer.GetData(args);
		Debug.Log("Vertex Count: " + args[0]);
		args[0] *= 3; 
		c.VertCount = args[0] * 3;

		c.ArgBufferRequest = UnityEngine.Experimental.Rendering.AsyncGPUReadback.Request(c.ArgBuffer);
		c.ArgBuffer.SetData(args);
    }

	public void DestroyChunk(GPUChunk c) {
		c.ArgBuffer.Release();
		c.AppendVertexBuffer.Release();
	}

	public void OnDestroy()
	{
		for(int i = 0; i < ChunkCount; i++) {
			GPUChunk c = Chunks[i];
			c.ArgBuffer.Release();
			c.AppendVertexBuffer.Release();
		}
	}

}