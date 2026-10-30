using System;

namespace DeskOrder.Core
{
    public static class Fft
    {
        public static void Transform(float[] re, float[] im)
        {
            int n = re.Length;
            if (n <= 1) return;
            if ((n & (n - 1)) != 0) throw new ArgumentException("FFT length must be a power of two");
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    float tr = re[i]; re[i] = re[j]; re[j] = tr;
                    float ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2.0 * Math.PI / len;
                float wReal = (float)Math.Cos(angle);
                float wImag = (float)Math.Sin(angle);
                for (int i = 0; i < n; i += len)
                {
                    float curReal = 1.0f, curImag = 0.0f;
                    int half = len >> 1;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k;
                        int b = a + half;
                        float ur = re[a], ui = im[a];
                        float vr = re[b] * curReal - im[b] * curImag;
                        float vi = re[b] * curImag + im[b] * curReal;
                        re[a] = ur + vr; im[a] = ui + vi;
                        re[b] = ur - vr; im[b] = ui - vi;
                        float nextReal = curReal * wReal - curImag * wImag;
                        curImag = curReal * wImag + curImag * wReal;
                        curReal = nextReal;
                    }
                }
            }
        }

        public static void Hann(float[] data)
        {
            int n = data.Length;
            if (n <= 1) return;
            for (int i = 0; i < n; i++)
            {
                double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
                data[i] *= (float)w;
            }
        }

        public static int DominantBand(float[] samples, int sampleRate, int bands)
        {
            int n = 1024;
            if (samples.Length < n) return -1;
            float[] re = new float[n];
            float[] im = new float[n];
            Array.Copy(samples, samples.Length - n, re, 0, n);
            Hann(re);
            Transform(re, im);
            int best = -1;
            double bestMag = 0;
            for (int i = 1; i < n / 2; i++)
            {
                double mag = re[i] * re[i] + im[i] * im[i];
                if (mag > bestMag) { bestMag = mag; best = i; }
            }
            if (best <= 0) return -1;
            double freq = (double)best * sampleRate / n;
            return BandOf(freq, bands);
        }

        public static int BandOf(double frequency, int bands)
        {
            const double lo = 40.0;
            const double hi = 16000.0;
            if (frequency <= lo) return 0;
            if (frequency >= hi) return bands - 1;
            double pos = Math.Log(frequency / lo) / Math.Log(hi / lo) * bands;
            int b = (int)pos;
            if (b < 0) b = 0;
            if (b >= bands) b = bands - 1;
            return b;
        }
    }
}
