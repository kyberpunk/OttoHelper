using System.Collections.Generic;
using System.Threading.Tasks;

namespace OttoHelper
{
    public class AsyncAutoResetEvent<T>
    {
        private readonly Queue<TaskCompletionSource<T>> _waits = new Queue<TaskCompletionSource<T>>();
        private bool _signaled;
        public bool Released
        {
            get
            {
                lock (_waits)
                {
                    return _waits.Count == 0;
                }
            }
        }

        public Task<T> WaitAsync()
        {
            lock (_waits)
            {
                if (_signaled)
                {
                    _signaled = false;
                    return Task.FromResult(default(T));
                }

                var tcs = new TaskCompletionSource<T>();
                _waits.Enqueue(tcs);
                return tcs.Task;
            }
        }

        public void Set(T result)
        {
            TaskCompletionSource<T> toRelease = null;

            lock (_waits)
            {
                if (_waits.Count > 0)
                    toRelease = _waits.Dequeue();
                else if (!_signaled)
                    _signaled = true;
            }

            toRelease?.SetResult(result);
        }
    }
}
