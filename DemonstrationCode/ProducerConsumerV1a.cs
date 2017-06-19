using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DemonstrationCode
{
    public class ProducerConsumerV1a<T>
    {
        private object LockObject = new object();
        private Task ConsumerTask { get; set; }
        private CancellationToken Token;
        private ManualResetEvent StartConsumer { get; set; } = new ManualResetEvent(false);
        private List<T> ProduceArray = new List<T>();

        public ProducerConsumerV1a(Action<T> consume, CancellationToken token)
        {
            ConsumerTask = Task.Run(() => ProcessItems(consume));
        }

        public void Add(T item)
        {
            lock(LockObject) {
                ProduceArray.Add(item);
                StartConsumer.Set();
            }
        }

        private void ProcessItems(Action<T> consume)
        {
            List<T> consumeArray;
            while(!Token.IsCancellationRequested) {
                StartConsumer.WaitOne();
                if(Token.IsCancellationRequested) {
                    return;
                }

                StartConsumer.Reset();

                lock(LockObject) {
                    consumeArray = ProduceArray.ToList();
                    ProduceArray.Clear();
                }

                foreach(var item in consumeArray) {
                    consume(item);
                }

                consumeArray.Clear();
            }
        }
    }
}
