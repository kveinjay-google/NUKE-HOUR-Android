using System;
using System.Linq;
using System.Text;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class RankedRegistrationForm
	{
		static bool Letter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ||
			c is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff' or >= '\uf900' and <= '\ufaff';

		public static string Validate(string username, string password, string confirmation)
		{
			var name = (username ?? "").Trim().Normalize(NormalizationForm.FormC);
			if (name.Length < 3 || name.Length > 24)
				return "label-ranked-error-username-length";
			if (!Letter(name[0]) || name.Any(c => !Letter(c) && !(c is >= '0' and <= '9') && c != '_'))
				return "label-ranked-error-username-format";
			if (password == null || password.Length < 12 || password.Length > 128)
				return "label-ranked-error-password-length";
			return password == confirmation ? null : "label-ranked-error-password-match";
		}

		public static void ApplyLayout(Widget content, bool ios, IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(ios, snapshot);
			var w = content.Bounds.Width;
			var h = content.Bounds.Height;
			var left = w * (policy.IsPhone ? 46 : 54) / 100;
			var width = w * (policy.IsPhone ? 96 : 93) / 100 - left;
			var height = Math.Max(policy.MinimumTarget, h * 9 / 100);
			var gap = policy.IsPhone ? Math.Max(3, h / 100) : Math.Max(6, h * 2 / 100);
			var y = policy.IsPhone ? Math.Max(policy.MinimumTarget + 12, h * 18 / 100) : h * 20 / 100;
			var labelWidth = width * 28 / 100;
			foreach (var row in new[] { ("USERNAME_LABEL", "USERNAME"), ("PASSWORD_LABEL", "PASSWORD"), ("CONFIRM_PASSWORD_LABEL", "CONFIRM_PASSWORD") })
			{
				content.Get(row.Item1).Bounds = new WidgetBounds(left, y, labelWidth, height);
				var field = content.Get(row.Item2);
				field.Bounds = new WidgetBounds(left + labelWidth, y, width - labelWidth, height);
				if (field is TextFieldWidget text)
				{ text.RankedPhoneMetalStyle = policy.IsPhone; text.LeftMargin = 12; text.RightMargin = 12; }
				y += height + gap;
			}

			var statusHeight = Math.Max(24, h * 18 / 100);
			content.Get("STATUS").Bounds = new WidgetBounds(left, y, width, statusHeight);
			y += statusHeight + gap;
			var buttonGap = Math.Max(8, w / 100);
			var half = (width - buttonGap) / 2;
			content.Get("REGISTER_BUTTON").Bounds = new WidgetBounds(left, y, half, height);
			content.Get("LOGIN_BUTTON").Bounds = new WidgetBounds(left + half + buttonGap, y, width - half - buttonGap, height);
		}
	}
}
