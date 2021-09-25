using System;
using System.Diagnostics;

namespace NTR {

    internal class FPSCounter {
        private static int MAXSAMPLES = 10;
        private Stopwatch stopwatch;
        private int tickindex = 0;
        private int[] ticklist;
        private int ticksum = 0;

        public FPSCounter() {
            stopwatch = Stopwatch.StartNew();
            ticklist = new int[MAXSAMPLES];
        }

        public double GetFPS() {
            int newtick = (int)stopwatch.ElapsedMilliseconds;

            ticksum -= ticklist[tickindex];  /* subtract value falling off */
            ticksum += newtick;              /* add new value */
            ticklist[tickindex] = newtick;   /* save new value so it can be subtracted later */
            if (++tickindex == MAXSAMPLES)    /* inc buffer index */
                tickindex = 0;

            stopwatch = Stopwatch.StartNew();
            return Math.Round((double)1000 / ((double)ticksum / MAXSAMPLES), 2); // return average
        }

        public bool SeemsAlive(long compare) {
            return (stopwatch.ElapsedMilliseconds < compare);
        }
    }
}