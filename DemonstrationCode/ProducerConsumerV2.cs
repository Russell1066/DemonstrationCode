using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DemonstrationCode
{
    public class ProducerConsumerV2<T>
    {
        private const int MINIMUM_SIZE = 64;
        private SpinLock Spinner = new SpinLock();
        private Task ConsumerTask { get; set; }
        private CancellationToken Token;
        private ManualResetEvent StartConsumer { get; set; } = new ManualResetEvent(false);
        private List<T> ProduceArray = new List<T>(MINIMUM_SIZE);
        List<T> ConsumeArray = new List<T>(MINIMUM_SIZE);

        public ProducerConsumerV2(Action<T> consume, CancellationToken token)
        {
            ConsumerTask = Task.Run(() => ProcessItems(consume));
        }

        public void Add(T item)
        {
            bool taken = false;
            try {
                Spinner.Enter(ref taken);
                ProduceArray.Add(item);
            } catch {
                if(taken) {
                    Spinner.Exit();
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
                if(ProduceArray.Capacity > ConsumeArray.Capacity) {
                    ConsumeArray.Capacity = ProduceArray.Capacity;
                }

                bool taken = false;
                try {
                    List<T> temp = ProduceArray;
                    ProduceArray = ConsumeArray;
                    ConsumeArray = temp;
                } catch {
                    if(taken) {
                        Spinner.Exit();
                    }
                }

                foreach(var item in ConsumeArray) {
                    consume(item);
                }

                ConsumeArray.Clear();
            }
        }
    }
}
