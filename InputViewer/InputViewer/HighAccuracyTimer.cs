using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SuzumeInputViewer
{

    public class HighAccuracyTimer
    {

        /// <summary>
        /// タイマーイベントが発生する周期（単位はミリ秒）
        /// </summary>
        public double interval { get; protected set; }

        /// <summary>
        /// タイマーが開始している場合はtrue
        /// </summary>
        public bool enabled { get; protected set; }

        /// <summary>
        /// タイマーイベント
        /// </summary>
        public event d_ElapsedEvent Elapsed;

        /// <summary>
        /// タイマーイベントが発生する周期（単位はtick）
        /// </summary>
        private long intervalInTick;

        [DllImport("winmm.dll", ExactSpelling = true, CharSet = CharSet.Ansi)]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll", ExactSpelling = true, CharSet = CharSet.Ansi)]
        private static extern uint timeEndPeriod(uint uPeriod);

        private CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="interval">タイマーイベントが発生する周期(単位はミリ秒)</param>
        public HighAccuracyTimer(double interval)
        {
            this.interval = interval;
            this.intervalInTick = MillisecToTick(interval);
        }

        /// <summary>
        /// タイマーを開始する
        /// </summary>
        public void Start()
        {
            Task.Run(() =>
            {
                enabled = true;
                timeBeginPeriod(1);
                Stopwatch sw = new Stopwatch();
                long period = 0;  // in Tick

                sw.Start();
                while (!(cts.IsCancellationRequested))
                {
                    // 次にタイマーイベントが発生するタイミングを決める
                    while (period < sw.ElapsedTicks)
                        period += intervalInTick;

                    // periodの直前まで待つ
                    int millisecond = (int)TickToMillisec(period - sw.ElapsedTicks) - 1;
                    if (millisecond > 0) Thread.Sleep(millisecond);

                    // 残りの細かい時間を待つ
                    while (period > sw.ElapsedTicks) { }

                    // タイマーイベントを発生させる
                    Elapsed(sw.ElapsedTicks);
                }
                sw.Stop();
            });
        }

        /// <summary>
        /// タイマーを停止する
        /// </summary>
        public void Stop()
        {
            cts.Cancel();
            if (enabled) timeEndPeriod(1);
            enabled = false;
        }

        private double TickToMillisec(long tick)
        {
            return (double)tick / Stopwatch.Frequency * 1000;
        }

        private long MillisecToTick(double millisec)
        {
            return (long)(millisec * Stopwatch.Frequency / 1000);
        }

    }

}
