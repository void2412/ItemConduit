using ItemConduit.Nodes;
using UnityEngine;
using UnityEngine.UI;

namespace ItemConduit.GUI
{
	public class InsertNodeGUI : FilterNodeGUI<InsertNode>
	{
		private InputField priorityInput;

		protected override string GetTitleText() => "Insert Node Configuration";
		protected override float GetChannelInputWidth() => 120f;
		protected override float GetTopRowSpacing() => 10f;

		protected override void AddTopRowContent(Transform topRow)
		{
			CreateLabel(topRow, "PriorityLabel", "Priority:", 60f);

			GameObject priorityInputObj = CreateInputField(topRow, "0", 60f);
			priorityInput = priorityInputObj.GetComponent<InputField>();
			priorityInput.contentType = InputField.ContentType.IntegerNumber;
			priorityInput.onEndEdit.AddListener(OnPriorityChanged);
		}

		protected override void OnAfterLoadNodeSettings()
		{
			if (priorityInput != null)
			{
				priorityInput.SetTextWithoutNotify(node.Priority.ToString());
			}
		}

		private void OnPriorityChanged(string priorityText)
		{
			if (node != null && int.TryParse(priorityText, out int priority))
			{
				node.SetPriority(priority);
			}
		}
	}
}