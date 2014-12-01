﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BosunReporter;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;

            Func<Uri> getUrl = () =>
            {
                return new Uri("http://192.168.59.104:8070/");
            };

            var options = new BosunReporterOptions()
            {
                MetricsNamePrefix = "bret.",
                GetBosunUrl = getUrl,
                ThrowOnPostFail = true,
                ReportingInterval = 5,
                PropertyToTagName = NameTransformers.CamelToLowerSnakeCase
            };
            var reporter = new BosunReporter.BosunReporter(options);

            reporter.BindMetric("my_counter", typeof(TestCounter));
            var counter = reporter.GetMetric<TestCounter>("my_counter");
            counter.Increment();
            counter.Increment();

            var gauge = reporter.GetMetric("my_gauge", new TestAggregateGauge("1"));
            if (gauge != reporter.GetMetric("my_gauge", new TestAggregateGauge("1")))
                throw new Exception("WAT?");

            //reporter.GetMetric<TestAggregateGauge>("my_gauge_95"); // <- should throw an exception

            var gauge2 = reporter.GetMetric("my_gauge", new TestAggregateGauge("2"));
            for (var i = 0; i < 6; i++)
            {
                new Thread(Run).Start(new Tuple<BosunAggregateGauge, BosunAggregateGauge, int>(gauge, gauge2, i));
            }

            var si = 0;
            var snapshot = reporter.GetMetric("my_snapshot", new TestSnapshotGauge(() => ++si%5));

            Thread.Sleep(TimeSpan.FromSeconds(16));
            Console.WriteLine("removing...");
            Console.WriteLine(reporter.RemoveMetric(counter));
        }

        static void Run(object obj)
        {
            var tup = (Tuple<BosunAggregateGauge, BosunAggregateGauge, int>)obj;
            var gauge1 = tup.Item1;
            var gauge2 = tup.Item2;

            if (gauge1 == gauge2)
                throw new Exception("These weren't supposed to be the same... they have different tags.");

            var rand = new Random(tup.Item3);
            int i;
            while (true)
            {
                for (i = 0; i < 20; i++)
                {
                    gauge1.Record(rand.NextDouble());
                    gauge2.Record(rand.NextDouble());
                }
                Thread.Sleep(1);
            }
        }
    }

    [GaugeAggregator(AggregateMode.Average)]
    [GaugeAggregator(AggregateMode.Max)]
    [GaugeAggregator(AggregateMode.Min)]
    [GaugeAggregator(AggregateMode.Median)]
    [GaugeAggregator(AggregateMode.Percentile, 0.95)]
    [GaugeAggregator(AggregateMode.Percentile, 0.25)]
    public class TestAggregateGauge : BosunAggregateGauge
    {
        [BosunTag] public readonly string Host;
        [BosunTag] public readonly string SomeTagName;

        public TestAggregateGauge(string something)
        {
            Host = "bret-host";
            SomeTagName = something;
        }
    }

    public class TestCounter : BosunCounter
    {
        [BosunTag]
        public readonly string Host;

        public TestCounter()
        {
            Host = "bret-host";
        }
    }

    public class TestSnapshotGauge : BosunSnapshotGauge
    {
        [BosunTag] public readonly string Host;

        public Func<double> GetValueLambda;

        public TestSnapshotGauge(Func<double> getValue)
        {
            Host = "bret-host";
            GetValueLambda = getValue;
        }

        protected override double? GetValue()
        {
            return GetValueLambda();
        }
    }
}
