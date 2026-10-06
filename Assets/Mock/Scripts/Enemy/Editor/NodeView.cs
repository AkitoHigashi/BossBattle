using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviorTree.Mock.Editor
{
    public class NodeView : Node
    {
        public Port Input;
        public Port Output;

        public NodeView(string title, Vector2 position, string typeClass)
            : base("Assets/Mock/UIBuilder/NodeView.uxml")
        {
            this.title = title;
            SetPosition(new Rect(position, Vector2.zero));
            AddToClassList(typeClass);

            Input = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            Input.portName = "";
            inputContainer.Add(Input);

            var capacity = typeClass == "composite" ? Port.Capacity.Multi : Port.Capacity.Single;
            Output = InstantiatePort(Orientation.Vertical, Direction.Output, capacity, typeof(bool));
            Output.portName = "";
            outputContainer.Add(Output);
        }
    }
}
