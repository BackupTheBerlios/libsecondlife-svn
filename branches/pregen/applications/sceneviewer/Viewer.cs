using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using libsecondlife;

namespace sceneviewer
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    partial class Viewer : Microsoft.Xna.Framework.Game
    {
        private SecondLife Client;
        private List<PrimObject> Prims;

        // The shader effect that we're loading
        private Effect Effect;

        // Variables describing the shapes being drawn
        private VertexDeclaration VertexDeclaration;
        private List<VertexPositionColor[]> DisplayPrims;
        private VertexPositionColor[] surface;

        // Matrices
        private Matrix WorldViewProjection;
        private Matrix World, Projection;
        private Camera Camera;

        // Variables for keeping track of the state of the mouse
        private ButtonState PreviousLeftButton;
        private Vector2 PreviousMousePosition;

        private int seed = 0;

        public Viewer()
        {
            Camera = new Camera(new Vector3(5.0f, 0.0f, 0.0f), Vector3.Zero);
            PreviousLeftButton = ButtonState.Released;

            Prims = new List<PrimObject>();
            DisplayPrims = new List<VertexPositionColor[]>();

            Hashtable loginParams = NetworkManager.DefaultLoginValues("Ron", "Hubbard",
                "radishes", "00:00:00:00:00:00", "last", 1, 50, 50, 50, "Win", "0",
                "botmanager", "contact@libsecondlife.org");

            Client = new SecondLife();

            Client.Objects.OnNewPrim += new NewPrimCallback(OnNewPrim);

            if (!Client.Network.Login(loginParams))
            {
                Exit();
            }

            InitializeComponent();
            InitializeTransform();
            InitializeEffect();
            InitializeScene();
        }

        void OnNewPrim(Simulator simulator, PrimObject prim, ulong regionHandle, ushort timeDilation)
        {
            Prims.Add(prim);

            VertexPositionColor[] primDisplay = new VertexPositionColor[36];

            Vector3 topLeftFront = new Vector3(
                prim.Position.X - (prim.Scale.X / 2.0f), 
                prim.Position.Y + (prim.Scale.Y / 2.0f), 
                prim.Position.Z + (prim.Scale.Z / 2.0f));
            Vector3 bottomLeftFront = new Vector3(
                prim.Position.X - (prim.Scale.X / 2.0f),
                prim.Position.Y - (prim.Scale.Y / 2.0f),
                prim.Position.Z + (prim.Scale.Z / 2.0f));
            Vector3 topRightFront = new Vector3(
                prim.Position.X + (prim.Scale.X / 2.0f),
                prim.Position.Y + (prim.Scale.Y / 2.0f),
                prim.Position.Z + (prim.Scale.Z / 2.0f));
            Vector3 bottomRightFront = new Vector3(
                prim.Position.X + (prim.Scale.X / 2.0f),
                prim.Position.Y - (prim.Scale.Y / 2.0f),
                prim.Position.Z + (prim.Scale.Z / 2.0f));
            Vector3 topLeftBack = new Vector3(
                prim.Position.X - (prim.Scale.X / 2.0f),
                prim.Position.Y + (prim.Scale.Y / 2.0f),
                prim.Position.Z - (prim.Scale.Z / 2.0f));
            Vector3 topRightBack = new Vector3(
                prim.Position.X + (prim.Scale.X / 2.0f),
                prim.Position.Y + (prim.Scale.Y / 2.0f),
                prim.Position.Z - (prim.Scale.Z / 2.0f));
            Vector3 bottomLeftBack = new Vector3(
                prim.Position.X - (prim.Scale.X / 2.0f),
                prim.Position.Y - (prim.Scale.Y / 2.0f),
                prim.Position.Z - (prim.Scale.Z / 2.0f));
            Vector3 bottomRightBack = new Vector3(
                prim.Position.X + (prim.Scale.X / 2.0f),
                prim.Position.Y - (prim.Scale.Y / 2.0f),
                prim.Position.Z - (prim.Scale.Z / 2.0f));

            Random rand = new Random(seed);
            byte r = (byte)rand.Next(256);
            byte g = (byte)rand.Next(256);
            byte b = (byte)rand.Next(256);
            seed = rand.Next();
            Color color = new Color(r, g, b);

            // Front face
            primDisplay[0] =
                new VertexPositionColor(topLeftFront, color);
            primDisplay[1] =
                new VertexPositionColor(bottomLeftFront, color);
            primDisplay[2] =
                new VertexPositionColor(topRightFront, color);
            primDisplay[3] =
                new VertexPositionColor(bottomLeftFront, color);
            primDisplay[4] =
                new VertexPositionColor(bottomRightFront, color);
            primDisplay[5] =
                new VertexPositionColor(topRightFront, color);

            // Back face 
            primDisplay[6] =
                new VertexPositionColor(topLeftBack, color);
            primDisplay[7] =
                new VertexPositionColor(topRightBack, color);
            primDisplay[8] =
                new VertexPositionColor(bottomLeftBack, color);
            primDisplay[9] =
                new VertexPositionColor(bottomLeftBack, color);
            primDisplay[10] =
                new VertexPositionColor(topRightBack, color);
            primDisplay[11] =
                new VertexPositionColor(bottomRightBack, color);

            // Top face
            primDisplay[12] =
                new VertexPositionColor(topLeftFront, color);
            primDisplay[13] =
                new VertexPositionColor(topRightBack, color);
            primDisplay[14] =
                new VertexPositionColor(topLeftBack, color);
            primDisplay[15] =
                new VertexPositionColor(topLeftFront, color);
            primDisplay[16] =
                new VertexPositionColor(topRightFront, color);
            primDisplay[17] =
                new VertexPositionColor(topRightBack, color);

            // Bottom face 
            primDisplay[18] =
                new VertexPositionColor(bottomLeftFront, color);
            primDisplay[19] =
                new VertexPositionColor(bottomLeftBack, color);
            primDisplay[20] =
                new VertexPositionColor(bottomRightBack, color);
            primDisplay[21] =
                new VertexPositionColor(bottomLeftFront, color);
            primDisplay[22] =
                new VertexPositionColor(bottomRightBack, color);
            primDisplay[23] =
                new VertexPositionColor(bottomRightFront, color);

            // Left face
            primDisplay[24] =
                new VertexPositionColor(topLeftFront, color);
            primDisplay[25] =
                new VertexPositionColor(bottomLeftBack, color);
            primDisplay[26] =
                new VertexPositionColor(bottomLeftFront, color);
            primDisplay[27] =
                new VertexPositionColor(topLeftBack, color);
            primDisplay[28] =
                new VertexPositionColor(bottomLeftBack, color);
            primDisplay[29] =
                new VertexPositionColor(topLeftFront, color);

            // Right face 
            primDisplay[30] =
                new VertexPositionColor(topRightFront, color);
            primDisplay[31] =
                new VertexPositionColor(bottomRightFront, color);
            primDisplay[32] =
                new VertexPositionColor(bottomRightBack, color);
            primDisplay[33] =
                new VertexPositionColor(topRightBack, color);
            primDisplay[34] =
                new VertexPositionColor(topRightFront, color);
            primDisplay[35] =
                new VertexPositionColor(bottomRightBack, color);

            lock (DisplayPrims)
            {
                DisplayPrims.Add(primDisplay);
            }
        }

        private void InitializeTransform()
        {
            // set the World matrix to something
            World = Matrix.CreateTranslation(Vector3.Zero);

            // build a pretty standard projection matrix
            Projection = Matrix.CreatePerspectiveFieldOfView(
                (float)Math.PI / 4.0f,  // 45 degrees
                (float)this.Window.ClientWidth / (float)this.Window.ClientHeight,
                1.0f, 512.0f);

            WorldViewProjection = World * Camera.ViewMatrix * Projection;
        }

        private void InitializeEffect()
        {
            CompiledEffect compiledEffect = Effect.CompileEffectFromFile(
                "ReallySimpleEffect.fx", null, null,
                CompilerOptions.Debug |
                CompilerOptions.SkipOptimization,
                TargetPlatform.Windows);

            Effect = new Effect(graphics.GraphicsDevice,
                compiledEffect.GetShaderCode(), CompilerOptions.None,
                null);
        }

        private void InitializeScene()
        {
            VertexDeclaration = new VertexDeclaration(
                graphics.GraphicsDevice, VertexPositionColor.VertexElements);

            /*surface = new VertexPositionColor[6];

            Vector3 ul = new Vector3(256, 256, 0);
            Vector3 ur = new Vector3(256, 0, 0);
            Vector3 bl = new Vector3(0, 256, 0);
            Vector3 br = new Vector3(0, 0, 0);

            surface[0] = new VertexPositionColor(ul, Color.White);
            surface[1] = new VertexPositionColor(bl, Color.White);
            surface[2] = new VertexPositionColor(ur, Color.White);
            surface[3] = new VertexPositionColor(bl, Color.White);
            surface[4] = new VertexPositionColor(br, Color.White);
            surface[5] = new VertexPositionColor(ur, Color.White);*/
        }

        protected override void Update()
        {
            // The time since Update was called last
            float elapsed = (float)ElapsedTime.TotalSeconds;

            MouseState currentState = Mouse.GetState();

            Camera.Zoom = currentState.ScrollWheelValue * 0.005f;

            if (currentState.LeftButton == ButtonState.Pressed &&
                PreviousLeftButton == ButtonState.Pressed)
            {
                Vector2 curMouse = new Vector2(currentState.X, currentState.Y);
                Vector2 deltaMouse = PreviousMousePosition - curMouse;

                Camera.Theta += deltaMouse.X * 0.01f;
                Camera.Phi -= deltaMouse.Y * 0.005f;
                PreviousMousePosition = curMouse;
            }
            // It's implied that the leftPreviousState is unpressed in this situation.
            else if (currentState.LeftButton == ButtonState.Pressed)
            {
                PreviousMousePosition = new Vector2(currentState.X, currentState.Y);
            }

            PreviousLeftButton = currentState.LeftButton;

            // Let the GameComponents update
            UpdateComponents();
        }

        protected override void Draw()
        {
            // Make sure we have a valid device
            if (!graphics.EnsureDevice())
                return;

            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);
            graphics.GraphicsDevice.BeginScene();

            // Let the GameComponents draw
            DrawComponents();

            WorldViewProjection = World * Camera.ViewMatrix * Projection;
            Effect.Parameters["WorldViewProj"].SetValue(WorldViewProjection);
            Effect.CurrentTechnique = Effect.Techniques["TransformTechnique"];
            Effect.CommitChanges();

            graphics.GraphicsDevice.VertexDeclaration = VertexDeclaration;
            graphics.GraphicsDevice.RenderState.CullMode = CullMode.CullClockwiseFace;

            Effect.Begin(EffectStateOptions.Default);
            foreach (EffectPass pass in Effect.CurrentTechnique.Passes)
            {
                pass.Begin();

                //graphics.GraphicsDevice.DrawUserPrimitives<VertexPositionColor>
                //            (PrimitiveType.TriangleList, 12, surface);

                lock (DisplayPrims)
                {
                    foreach (VertexPositionColor[] vpc in DisplayPrims)
                    {
                        graphics.GraphicsDevice.DrawUserPrimitives<VertexPositionColor>
                            (PrimitiveType.TriangleList, 12, vpc);
                    }
                }

                pass.End();
            }
            Effect.End();

            graphics.GraphicsDevice.EndScene();
            graphics.GraphicsDevice.Present();
        }
    }
}
