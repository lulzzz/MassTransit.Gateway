using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace MassTransit.Gateway
{
    public static class ObservableEx
    {
        public static IObservable<T> Create<T>(Func<Action<T>, Task> subscribe) =>
            Observable.Create<T>(o =>
            {
                var task = subscribe(o.OnNext);
                var subscription =
                    task.ToObservable().Subscribe(_ => { }, o.OnError, o.OnCompleted);
                return new CompositeDisposable(task, subscription);
            });
    }
}