//await Basic.RunAsync();
await Generator.RunAsync();
public static class Basic
{
    public static async Task RunAsync()
    {
        var ch = Channel.CreateUnbounded<string>();

        var consumer = Task.Run(async () =>
        {
            await foreach (var item in ch.Reader.ReadAllAsync())
                Console.WriteLine(item);
        });

        var producer = Task.Run(async () =>
        {
            var rnd = new Random();
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3)));
                await ch.Writer.WriteAsync($"Message {i}");
            }
            ch.Writer.Complete();
        });

        await Task.WhenAll(producer, consumer);

        Console.ReadKey();
    }
}
public static class Generator
{
    public static async Task RunAsync()
    {
        var pom = CreateMessenger("Pom", 10);
        var readers = Split<String>(pom, 3);
        var tasks = new List<Task>();

        for (int i = 0; i < readers.Count; i++)
        {
            var reader = readers[i];
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await foreach (var item in reader.ReadAllAsync())
                    Console.WriteLine($"Reader {index}: {item}");
            }));
        }

        await Task.WhenAll(tasks);
    }
    static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
    {
        var output = Channel.CreateUnbounded<T>();

        Task.Run(async () =>
        {
            async Task Redirect(ChannelReader<T> input)
            {
                await foreach (var item in input.ReadAllAsync())
                    await output.Writer.WriteAsync(item);
            }

            await Task.WhenAll(inputs.Select(i => Redirect(i)).ToArray());
            output.Writer.Complete();
        });

        return output;
    }
    static IList<ChannelReader<T>> Split<T>(ChannelReader<T> ch, int n)
    {
        var outputs = new Channel<T>[n];

        for (int i = 0; i < n; i++)
            outputs[i] = Channel.CreateUnbounded<T>();

        Task.Run(async () =>
        {
            var index = 0;
            await foreach (var item in ch.ReadAllAsync())
            {
                await outputs[index].Writer.WriteAsync(item);
                index = (index + 1) % n;
            }

            foreach (var ch in outputs)
                ch.Writer.Complete();
        });

        return outputs.Select(ch => ch.Reader).ToArray();
    }
    static ChannelReader<string> CreateMessenger(string msg, int count)
    {
        var ch = Channel.CreateUnbounded<string>();
        var rnd = new Random();

        Task.Run(async () =>
        {
            for (int i = 0; i < count; i++)
            {
                await ch.Writer.WriteAsync($"{msg} {i}");
                await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3)));
            }
            ch.Writer.Complete();
        });

        return ch.Reader;
    }
}