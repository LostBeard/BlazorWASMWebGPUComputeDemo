using BlazorWASMWebGPUComputeDemo.Services;
using Microsoft.AspNetCore.Components;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using System.Runtime.InteropServices;

namespace BlazorWASMWebGPUComputeDemo.Pages.ComputeBoidsSample
{
    public partial class ComputeBoids : IDisposable
    {
        [Inject]
        BlazorJSRuntime JS { get; set; } = default!;

        [Inject]
        ShaderLoader ShaderLoader { get; set; } = default!;

        string _log = "Ready";
        bool _running = false;

        ElementReference canvasRef;

        long numParticles = 1500;
        HTMLCanvasElement? canvas;
        GPUAdapter? adapter;
        GPUDevice? device;
        GPUBuffer? simParamBuffer;
        GPUBuffer[] particleBuffers = new GPUBuffer[0];
        GPUBindGroup[] particleBindGroups = new GPUBindGroup[0];
        GPUComputePipeline? computePipeline;
        GPURenderPassDescriptor? renderPassDescriptor;
        GPUComputePassDescriptor? computePassDescriptor;
        GPUBuffer? spriteVertexBuffer;
        Window? window;
        GPUCanvasContext? context;
        GPURenderPipeline? renderPipeline;

        bool Disposed = false;

        long t = 0;
        double computePassDurationSum = 0;
        double renderPassDurationSum = 0;
        double timerSamples = 0;

        static Random random = new Random();

        void Log(string msg = "", bool clear = false)
        {
            if (clear) _log = "";
            _log += msg + "\n";
            StateHasChanged();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await Init();
                Frame();
            }
        }
        public void Dispose()
        {
            Disposed = true;
        }
        void CleanUp()
        {
            canvas?.Dispose();
            adapter?.Dispose();
            device?.Dispose();
            simParamBuffer?.Dispose();
            foreach(var particleBuffer in particleBuffers) particleBuffer.Dispose();
            particleBuffers = new GPUBuffer[0];
            foreach (var particleBindGroup in particleBindGroups) particleBindGroup.Dispose();
            particleBindGroups = new GPUBindGroup[0];
            computePipeline?.Dispose();
            spriteVertexBuffer?.Dispose();
            window?.Dispose();
            context?.Dispose();
            renderPipeline?.Dispose();
        }
        void Frame()
        {
            try
            {
                _Frame();
            }
            catch (Exception ex)
            {
                JS.Log("Frame failed:", ex.ToString());
            }
        }
        void _Frame()
        {
            if (Disposed)
            {
                CleanUp();
                return;
            }
            //
            renderPassDescriptor!.ColorAttachments[0].View = context!.GetCurrentTexture().CreateView();

            using var commandEncoder = device!.CreateCommandEncoder();
            {
                using var passEncoder = commandEncoder.BeginComputePass(computePassDescriptor);
                passEncoder.SetPipeline(computePipeline!);
                passEncoder.SetBindGroup(0, particleBindGroups[t % 2]);
                passEncoder.DispatchWorkgroups((uint)Math.Ceiling(numParticles / 64f));
                passEncoder.End();
            }
            {
                using var passEncoder = commandEncoder.BeginRenderPass(renderPassDescriptor);
                passEncoder.SetPipeline(renderPipeline!);
                passEncoder.SetVertexBuffer(0, particleBuffers[(t + 1) % 2]);
                passEncoder.SetVertexBuffer(1, spriteVertexBuffer);
                passEncoder.Draw(3, (uint)numParticles, 0, 0);
                passEncoder.End();
            }

            // Submit the command queue
            using var commandQueue = commandEncoder.Finish();
            device.Queue.Submit([commandQueue]);

            //
            if (Disposed)
            {
                CleanUp();
                return;
            }
            ++t;
            window!.RequestAnimationFrame(() => Frame());
        }
        async Task Init()
        {
            canvas = new HTMLCanvasElement(canvasRef);
            window = JS.Get<Window>("window");
            using var document = JS.Get<Document>("document");
            using var navigator = JS.Get<Navigator>("navigator");
            using var gpu = navigator.Gpu;
            if (gpu == null)
            {
                Log("WebGPU not supported");
                return;
            }

            adapter = await gpu.RequestAdapter(new GPURequestAdapterOptions
            {
                FeatureLevel = "compatibility"
            });
            if (adapter == null)
            {
                Log("WebGPU not supported");
                return;
            }

            var hasTimestampQuery = adapter.Features.Has("timestamp-query");
            device = await adapter.RequestDevice(new GPUDeviceDescriptor
            {
                RequiredFeatures = hasTimestampQuery ? new List<string> { "timestamp-query" } : null,
            });
            if (device == null)
            {
                Log("WebGPU not supported");
                return;
            }

            var perfDisplayContainer = document.CreateElement<HTMLDivElement>("div");
            perfDisplayContainer.Style["color"] = "white";
            perfDisplayContainer.Style["background"] = "black";
            perfDisplayContainer.Style["position"] = "absolute";
            perfDisplayContainer.Style["bottom"] = "10px";
            perfDisplayContainer.Style["left"] = "10px";
            perfDisplayContainer.Style["textAlign"] = "left";

            var perfDisplay = document.CreateElement<HTMLElement>("pre");
            perfDisplay.Style["margin"] = ".5em";
            perfDisplayContainer.AppendChild(perfDisplay);
            canvas.ParentNode!.AppendChild(perfDisplayContainer);

            context = canvas.GetWebGPUContext();
            var devicePixelRatio = window.DevicePixelRatio;
            canvas.Width = (int)Math.Round(canvas.ClientWidth * devicePixelRatio);
            canvas.Height = (int)Math.Round(canvas.ClientHeight * devicePixelRatio);
            var presentationFormat = gpu.GetPreferredCanvasFormat();

            context.Configure(new GPUCanvasConfiguration
            {
                Device = device,
                Format = presentationFormat,
            });

            var spriteWGSL = await ShaderLoader.GetShaderString("sprite.wgsl");
            var spriteShaderModule = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = spriteWGSL! });
            renderPipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
            {
                Layout = GPUAutoLayoutMode.Auto,
                Vertex = new GPUVertexState
                {
                    Module = spriteShaderModule,
                    Buffers = new[] {
                        // instanced particles buffer
                        new GPUVertexBufferLayout {
                            ArrayStride = 4 * 4,
                            StepMode = GPUVertexStepMode.Instance,
                            Attributes = new[] {
                                // instance position
                                new GPUVertexAttribute {
                                    ShaderLocation = 0,
                                    Offset = 0,
                                    Format = GPUVertexFormat.Float32x2,
                                },
                                // instance velocity
                                new GPUVertexAttribute {
                                    ShaderLocation = 1,
                                    Offset = 2 * 4,
                                    Format = GPUVertexFormat.Float32x2,
                                },
                            }
                        },
                        // vertex buffer
                        new GPUVertexBufferLayout {
                            ArrayStride = 2 * 4,
                            StepMode = GPUVertexStepMode.Vertex,
                            Attributes= new[] {
                                // vertex positions
                                new GPUVertexAttribute
                                {
                                    ShaderLocation= 2,
                                    Offset= 0,
                                    Format = GPUVertexFormat.Float32x2,
                                }
                            }
                        }
                    }
                },
                Fragment = new GPUFragmentState
                {
                    Module = spriteShaderModule,
                    Targets = new[] {
                        new GPUColorTargetState {
                            Format = presentationFormat,
                        }
                    }
                },
                Primitive = new GPUPrimitiveState
                {
                    Topology = GPUPrimitiveTopology.TriangleList,
                }
            });

            var updateSpritesWGSL = await ShaderLoader.GetShaderString("updateSprites.wgsl");
            computePipeline = device.CreateComputePipeline(new GPUComputePipelineDescriptor
            {
                Layout = GPUAutoLayoutMode.Auto,
                Compute = new GPUProgrammableStage
                {
                    Module = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = updateSpritesWGSL! })
                }
            });

            renderPassDescriptor = new GPURenderPassDescriptor
            {
                ColorAttachments = new[] {
                    new GPURenderPassColorAttachment {
                        ClearValue = new double[] { 0, 0, 0, 1},
                        LoadOp = GPULoadOp.Clear,
                        StoreOp = GPUStoreOp.Store
                    }
                }
            };

            computePassDescriptor = new GPUComputePassDescriptor();

            if (hasTimestampQuery)
            {
                // omitted
            }

            var vertexBufferData = new float[] {
                -0.01f, -0.02f, 0.01f,
                -0.02f, 0.0f, 0.02f
            };

            spriteVertexBuffer = device.CreateBuffer(new GPUBufferDescriptor
            {
                Size = (ulong)(Marshal.SizeOf<float>() * vertexBufferData.Length),
                Usage = GPUBufferUsage.Vertex,
                MappedAtCreation = true,
            });


            using var vertexArrayBuffer = spriteVertexBuffer.GetMappedRange();
            using var vertexUint8Array = new Float32Array(vertexArrayBuffer);
            vertexUint8Array.Set(vertexBufferData);

            var tmppp = vertexUint8Array.ToArray();
            spriteVertexBuffer.Unmap();

            var simParamBufferSize = 7 * Float32Array.BYTES_PER_ELEMENT;
            simParamBuffer = device.CreateBuffer(new GPUBufferDescriptor
            {
                Size = (ulong)simParamBufferSize,
                Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
            });

            UpdateSimParams();


            // gui part skipped


            float[] initialParticleData = new float[numParticles * 4];
            for (var i = 0; i < numParticles; ++i)
            {
                initialParticleData[4 * i + 0] = 2 * (random.NextSingle() - 0.5f);
                initialParticleData[4 * i + 1] = 2 * (random.NextSingle() - 0.5f);
                initialParticleData[4 * i + 2] = 2 * (random.NextSingle() - 0.5f) * 0.1f;
                initialParticleData[4 * i + 3] = 2 * (random.NextSingle() - 0.5f) * 0.1f;
            }

            particleBuffers = new GPUBuffer[2];
            particleBindGroups = new GPUBindGroup[2];

            for (var i = 0; i < 2; ++i)
            {
                particleBuffers[i] = device.CreateBuffer(new GPUBufferDescriptor
                {
                    Size = (ulong)initialParticleData.ByteLength(),
                    Usage = GPUBufferUsage.Vertex | GPUBufferUsage.Storage,
                    MappedAtCreation = true,
                });
                new Float32Array(particleBuffers[i].GetMappedRange()).Set(
                    initialParticleData
                );
                particleBuffers[i].Unmap();
            }

            for (var i = 0; i < 2; ++i)
            {
                particleBindGroups[i] = device.CreateBindGroup(new GPUBindGroupDescriptor
                {
                    Layout = computePipeline.GetBindGroupLayout(0),
                    Entries = new[]
                    {
                        new GPUBindGroupEntry {
                            Binding = 0,
                            Resource =new GPUBufferBinding
                            {
                                Buffer = simParamBuffer,
                            },
                        },
                        new GPUBindGroupEntry{
                            Binding= 1,
                            Resource=
                            new GPUBufferBinding{
                                Buffer= particleBuffers[i],
                                Offset= 0,
                                Size= (ulong)initialParticleData.ByteLength(),
                            },
                        },
                        new GPUBindGroupEntry{
                            Binding= 2,
                            Resource=
                            new GPUBufferBinding{
                                Buffer= particleBuffers[(i + 1) % 2],
                                Offset= 0,
                                Size= (ulong)initialParticleData.ByteLength(),
                            },
                        },
                    }
                });
            }

        }

        SimulationParameters simParams = new SimulationParameters
        {
            DeltaT = 0.04f,
            Rule1Distance = 0.1f,
            Rule2Distance = 0.025f,
            Rule3Distance = 0.025f,
            Rule1Scale = 0.02f,
            Rule2Scale = 0.05f,
            Rule3Scale = 0.005f,
        };

        void UpdateSimParams()
        {
            using var float32Array = new Float32Array([
                    simParams.DeltaT,
                    simParams.Rule1Distance,
                    simParams.Rule2Distance,
                    simParams.Rule3Distance,
                    simParams.Rule1Scale,
                    simParams.Rule2Scale,
                    simParams.Rule3Scale,
                ]);
            device!.Queue.WriteBuffer(
                simParamBuffer!,
                0,
                float32Array
            );
        }
        class SimulationParameters
        {
            public float DeltaT { get; set; }
            public float Rule1Distance { get; set; }
            public float Rule2Distance { get; set; }
            public float Rule3Distance { get; set; }
            public float Rule1Scale { get; set; }
            public float Rule2Scale { get; set; }
            public float Rule3Scale { get; set; }
        }
    }
}
