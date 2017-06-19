using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DemonstrationCode
{
    public class ProducerConsumerV1<T>
    {
        private object LockObject = new object();
        private Task ConsumerTask { get; set; }
        private CancellationToken Token;
        private ManualResetEvent StartConsumer { get; set; } = new ManualResetEvent(false);
        private List<T> ProduceArray = new List<T>();

        public ProducerConsumerV1(Action<T> consume, CancellationToken token)
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
            while(!Token.IsCancellationRequested) {
                StartConsumer.WaitOne();
                if(Token.IsCancellationRequested) {
                    return;
                }

                StartConsumer.Reset();

                List<T> consumeArray = new List<T>();

                lock(LockObject) {
                    var temp = ProduceArray;
                    ProduceArray = consumeArray;
                    consumeArray = temp;
                }

                foreach(var item in consumeArray) {
                    consume(item);
                }
            }
        }
    }
}
