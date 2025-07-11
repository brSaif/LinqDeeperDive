using BenchmarkDotNet.Running;
using LinqDeeperDive.ChannelsImpl;

bool enableBenchmark = false;

#region ChannelsDemos

// await ChannelImpl.DefaultChannel.Run();
// await ChannelImpl.SimpleChannelImpl<int>.Run();
// await ChannelImpl.BuggyChannelImpl<int>.Run();
// await ChannelImpl.BuggyChannelSimpleFixImpl<int>.Run();
// await ChannelImpl.FixedBuggyChannelImpl<int>.Run();
await ChannelImpl.FinalChannelImpl<int>.Run();


#endregion

if (enableBenchmark)
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

return;