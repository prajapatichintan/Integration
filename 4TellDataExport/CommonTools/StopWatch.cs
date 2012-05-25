using System;

namespace _4_Tell.Utilities
{

	public class StopWatch
	{
		private long startTime = 0;
		private long lapStart = 0;
		private long lapEnd = 0;
		private long endTime = 0;
		private bool started = false;
		private bool ended = false;

		public StopWatch(bool start = false)
		{
			Reset();
			if (start) Start();
		}

		public void Reset()
		{
			startTime = 0;
			lapStart = 0;
			lapEnd = 0;
			endTime = 0;
			started = false;
			ended = false;
		}

		public void Start()
		{
			startTime = DateTime.Now.Ticks;
			lapStart = startTime;
			lapEnd = startTime;
			started = true;
			ended = false;
		}

		public string Lap()
		{
			if (!started) return "not started";

			lapEnd = DateTime.Now.Ticks;
			//TimeSpan lap = new TimeSpan(lapEnd - lapStart);
			long lap = lapEnd - lapStart;
			lapStart = lapEnd;
			return Format(lap);
		}

		public string Stop()
		{
			if (!started) return "not started";

			lapEnd = DateTime.Now.Ticks;
			//TimeSpan lap = new TimeSpan(lapEnd - lapStart);
			long lap = lapEnd - lapStart;
			endTime = lapEnd;
			ended = true;
			started = false;
			return Format(lap);
		}

		public string TotalTime //gets total for period up to last Lap or Stop command
		{
			get { return Format(lapEnd - startTime); }
		}

		private string Format(long ticks)
		{
			int ms = (int)(ticks / TimeSpan.TicksPerMillisecond);
			int s = ms / 1000;
			int m = s / 60;
			s %= 60;
			ms %= 1000;
			return string.Format("{0:0}:{1:00}.{2:000}", m, s, ms);
		}
	}
}