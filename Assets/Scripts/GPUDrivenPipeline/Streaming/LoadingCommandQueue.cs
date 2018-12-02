using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class LoadingCommandQueue
    {
        public class LinkNode
        {
            public LoadCommand command;
            public LinkNode lastLevel;
            public LinkNode nextLevel;
            public LinkNode(LoadCommand command)
            {
                this.command = command;
                nextLevel = null;
                lastLevel = null;
            }
        }
        public LinkNode start = null;
        public LinkNode last = null;
        public List<LinkNode> pool;
        public LoadingCommandQueue()
        {
            pool = new List<LinkNode>(20);
            for (int i = 0; i < 20; ++i)
            {
                pool.Add(new LinkNode(default));
            }
        }

        public void Run(ref PipelineBaseBuffer baseBuffer, PipelineResources resources)
        {
            if (start == null) return;
            if (!start.command.isInitialized)
            {
                start.command.initFunc();
                start.command.isInitialized = true;
            }
            if (start.command.load(ref baseBuffer, resources))
            {
                LinkNode last = start;
                last.lastLevel = null;
                last.nextLevel = null;
                if (last != null)
                    pool.Add(last);
                start = start.nextLevel;
                if (start != null)
                {
                    start.lastLevel = null;
                }
            }
        }

        public void Queue(LoadCommand command)
        {
            LinkNode node;
            if (pool.Count > 0)
            {
                node = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                node.command = command;
            }
            else
            {
                node = new LinkNode(command);
            }
            if (start == null || last == null)
            {
                start = node;
                last = node;
            }
            else
            {
                last.nextLevel = node;
                node.lastLevel = last;
                last = node;
            }
        }
    }
}