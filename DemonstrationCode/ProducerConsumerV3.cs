using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DemonstrationCode
{
    public class ProducerConsumerV3<T>
    {
        private const int MINIMUM_SIZE = 64;
        private SpinLock Spinner = new SpinLock();
        private Task ConsumerTask { get; set; }
        private CancellationToken Token;
        private ManualResetEvent StartConsumer { get; set; } = new ManualResetEvent(false);
        private Segment Head;
        private Segment ProduceSegment;
        private Segment ConsumeSegment;
        private bool IsAllocating;

        public ProducerConsumerV3(Action<T> consume, CancellationToken token)
        {
            Head = new Segment() { Next = new Segment() };
            ProduceSegment = Head;
            ConsumerTask = Task.Run(() => ProcessItems(consume));
        }

        public void Add(T item)
        {
            bool taken = false;
            try {
                Spinner.Enter(ref taken);
                if(ProduceSegment.Data.Count == ProduceSegment.Data.Capacity) {
                    ProduceSegment = ProduceSegment.Next;
                }

                ProduceSegment.Data.Add(item);
            } finally {
                if(taken) {
                    Spinner.Exit();
                }
            }

            // Allocate, do not allocate in the spin lock
            // Yes, it is possible to waste allocations this way
            // It ensures the least amount of time in the lock
            if(ProduceSegment.Next == null && !Interlocked.Exchange(ref IsAllocating, true)) {
                var next = new Segment();
                try {
                    Spinner.Enter(ref taken);
                    if(ProduceSegment.Next == null) {
                        ProduceSegment.Next = next;
                    }
                } finally {
                    if(taken) {
                        Spinner.Exit();
                    }
                }
            }

            StartConsumer.Set();
        }

        private void ProcessItems(Action<T> consume)
        {
            while(!Token.IsCancellationRequested) {
                StartConsumer.WaitOne();
                if(Token.IsCancellationRequested) {
                    return;
                }

                StartConsumer.Reset();

                var newProducer = new Segment() { Next = new Segment(), };

                bool taken = false;
                try {
                    Spinner.Enter(ref taken);
                    ConsumeSegment = Head;
                    Head = newProducer;
                    ProduceSegment = Head;
                } catch {
                    if(taken) {
                        Spinner.Exit();
                    }
                }

                while(ConsumeSegment != null && ConsumeSegment.Data.Count > 0) {
                    foreach(var item in ConsumeSegment.Data) {
                        consume(item);
                    }
                    ConsumeSegment = ConsumeSegment.Next;
                }
            }
        }

        private class Segment
        {
            public List<T> Data = new List<T>(ProducerConsumerV3<T>.MINIMUM_SIZE);
            public Segment Next = null;
        }
    }
}
