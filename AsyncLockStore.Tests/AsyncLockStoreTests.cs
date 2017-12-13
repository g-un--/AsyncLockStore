using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using System.Linq;

namespace AsyncLockStore.Tests
{
    public class AsyncLockStoreTests
    {
        [Fact]
        public async Task ShouldWaitForSameKey()
        {
            //Arrange
            var store = new LockStore();
            var tasks = new List<Task>();
            var log = new List<int>();
            var tasksCount = 16;
            var childTasksReached = Enumerable.Range(0, tasksCount).Select(_ => new TaskCompletionSource<bool>()).ToList();

            //Act
            for (var i = 0; i < tasksCount; i++)
            {
                var id = i;
                tasks.Add(Task.Run(async () =>
                {
                    childTasksReached[id].SetResult(true);
                    await Task.WhenAll(childTasksReached.Select(x => x.Task));
                    using (await store.GetTokenAsync("test"))
                    {
                        lock (log) { log.Add(id); };
                        await Task.Delay(10);
                        lock (log) { log.Add(id); };
                    }
                }));
            }
            await Task.WhenAll(tasks);

            //Assert
            for (var i = 0; i < tasksCount; i++)
            {
                log[2 * i].ShouldBe(log[2 * i + 1]);
            }
        }

        [Fact]
        public async Task ShouldNotWaitForDifferentKey()
        {
            //Arrange
            var store = new LockStore();
            var tasks = new List<Task>();
            var log = new List<int>();
            var tasksCount = 16;
            var childTasks = Enumerable.Range(0, tasksCount).Select(_ => new TaskCompletionSource<bool>()).ToList(); 

            //Act
            for (var i = 0; i < tasksCount; i++)
            {
                var id = i;
                tasks.Add(Task.Run(async () =>
                {
                    using (await store.GetTokenAsync("test" + id))
                    {
                        lock (log) { log.Add(id); };
                        childTasks[id].SetResult(true);
                        await Task.WhenAll(childTasks.Select(x => x.Task));
                        lock (log) { log.Add(id); };
                    }
                }));
            }
            await Task.WhenAll(tasks);

            //Assert
            log.Take(tasksCount).Distinct().Count().ShouldBe(tasksCount);
            log.Skip(tasksCount).Distinct().Count().ShouldBe(tasksCount);
        }
    }
}
