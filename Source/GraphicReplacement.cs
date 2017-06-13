using System;

namespace Verse
{
	/// <summary> Used to replace the pawn's graphic if a condition is met. </summary>
	public class GraphicReplacement
	{
		Graphic replacement;
		Func<bool> condition;

		public GraphicReplacement(Graphic replacement, Func<bool> condition)
		{
			this.condition = condition;
			this.replacement = replacement;
		}

		/// <summary> Try to replace the pawn's graphic. Returns bool indicating success. </summary>
		public bool TryReplace(PawnRenderer renderer)
		{
			// condition isn't satisfied
			if (condition() == false)
				return false; // keep checking

			// graphic already set as active
			if (replacement == renderer.graphics.nakedGraphic)
				return true;

			renderer.graphics.nakedGraphic = replacement;
			renderer.graphics.ClearCache();
			return true;
		}
	}
}
