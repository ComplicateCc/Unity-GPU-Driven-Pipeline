using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace MPipeline
{
    public class PipelineEventAttribute : Attribute
    {
        public bool preRender;
        public bool postRender;
        public PipelineEventAttribute(bool preRender, bool postRender)
        {
            this.preRender = preRender;
            this.postRender = postRender;
        }
    }
}