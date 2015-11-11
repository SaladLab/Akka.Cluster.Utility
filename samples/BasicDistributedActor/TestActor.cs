using System;
using Akka.Actor;

namespace BasicDistributedActor
{
    public class TestActor : ReceiveActor
    {
        public TestActor(string name)
        {
            Console.WriteLine($"TestActor.ctor({name})");
        }
    }
}
