namespace BlazorWASMWebGPUComputeDemo.Fractals
{
    /// <summary>
    /// Class for defining the presets.
    /// </summary>
    public class MandelbrotPlace
    {
        private readonly string _name;
        private readonly double _centerX;
        private readonly double _centerY;
        private double _scaling;
        private readonly ushort _iterations;
        private readonly ushort _antiAliasing;
        private readonly ushort _colorRed;
        private readonly ushort _colorGreen;
        private readonly ushort _colorBlue;

        public MandelbrotPlace(string name, double centerX, double centerY, double scaling, ushort iterations, ushort antiAliasing, ushort colorRed, ushort colorGreen, ushort colorBlue)
        {
            _name = name;
            _centerX = centerX;
            _centerY = centerY;
            _scaling = scaling;
            _iterations = iterations;
            _antiAliasing = antiAliasing;
            _colorRed = colorRed;
            _colorGreen = colorGreen;
            _colorBlue = colorBlue;
        }

        public string Name { get { return _name; } }
        public double CenterX { get { return _centerX; } }
        public double CenterY { get { return _centerY; } }
        public double Scaling { get { return _scaling; } set { _scaling = value; } }
        public ushort Iterations { get { return _iterations; } }
        public ushort AntiAliasing { get { return _antiAliasing; } }
        public ushort ColorRed { get { return _colorRed; } }
        public ushort ColorGreen { get { return _colorGreen; } }
        public ushort ColorBlue { get { return _colorBlue; } }
    }
}
