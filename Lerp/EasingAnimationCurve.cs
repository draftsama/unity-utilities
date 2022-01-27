// ReSharper disable PossibleLossOfFraction

using UnityEngine;

namespace Modules.Utilities
{
	public class EasingAnimationCurve : MonoBehaviour
	{
		public static AnimationCurve Linear()
		{
			float tan45 = Mathf.Tan(Mathf.Deg2Rad * 45);

			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(new Keyframe(0, 0, tan45, tan45));
			curve.AddKey(new Keyframe(1, 1, tan45, tan45));
			return curve;
		}
		public static AnimationCurve EaseInQuad(){

			AnimationCurve curve = new AnimationCurve();
			var maxKey = 30;
			for (var i = 0; i <= maxKey; i++) {
				var pos = i / maxKey;
				curve.AddKey (pos,  Mathf.Pow (pos, 2)  );

			}

			return curve;

		}

		public static AnimationCurve EaseOutQuad(){

			AnimationCurve curve = new AnimationCurve();
			var maxKey = 30;
			for (var i = 0; i <= maxKey; i++) {
				var pos = i / maxKey;

				curve.AddKey ( pos,-( Mathf.Pow (pos - 1, 2)-1) );

			}

			return curve;

		}

		public static AnimationCurve EaseInOutQuad()
		{
			AnimationCurve curve = new AnimationCurve();

			var maxKey = 30;
			for (var i = 0; i <= maxKey; i++) {

				var pos = i / maxKey;

				if (pos < 0.5f) {

					curve.AddKey (pos, 2f* Mathf.Pow (pos,2));
				} else {

					curve.AddKey (pos, -(2f*Mathf.Pow (pos - 1,2) -1));
				}


			}

			return curve;
		}



		public static AnimationCurve EaseOutBounce(){

			AnimationCurve curve = new AnimationCurve();
			var maxKey = 30;


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;

				if (pos < (1f / 2.75f)) {
					curve.AddKey (pos, (7.5625f * pos * pos));
				} else if (pos < (2f / 2.75f)) {
					curve.AddKey (pos, (7.5625f * (pos -= (1.5f / 2.75f)) * pos + 0.75f));
				} else if (pos < (2.5f / 2.75f)) {
					curve.AddKey (pos, (7.5625f * (pos -= (2.25f / 2.75f)) * pos + 0.9375f));
				} else {
					curve.AddKey (pos, (7.5625f * (pos -= (2.625f / 2.75f)) * pos + 0.984375f));
				}

			}

			return curve;
		}

		public static AnimationCurve EaseInBounce(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;

				if (pos < (1f / 2.75f)) {
					curve.AddKey (1-pos, -(7.5625f * pos * pos)+1);
				} else if (pos < (2f / 2.75f)) {
					curve.AddKey (1-pos,  -(7.5625f * (pos -= (1.5f / 2.75f)) * pos + 0.75f)+1);
				} else if (pos < (2.5f / 2.75f)) {
					curve.AddKey (1-pos, -(7.5625f * (pos -= (2.25f / 2.75f)) * pos + 0.9375f)+1);
				} else {
					curve.AddKey (1-pos, -(7.5625f * (pos -= (2.625f / 2.75f)) * pos + 0.984375f)+1);
				}

			}

			return curve;
		}

		public static AnimationCurve EaseInBack(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;
			var s = 1.70158f;


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;

				curve.AddKey (pos,pos*pos*((s+1f) * pos -s));

			}

			return curve;

		}

		public static AnimationCurve EaseOutBack(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;
			float s = 1.70158f;


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;

				curve.AddKey (pos, (-(1-pos)*(1-pos)*((s + 1f) * (1-pos) - s )) +1);

			}

			return curve;

		}

		public static AnimationCurve EaseInOutBack(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;
			float s = 1.70158f;


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;

				if (pos < 0.5f) {
					curve.AddKey (pos, 4f*( pos*pos*(((s * 1.525f)+1.8f) * pos -s) ));

				} else {

					curve.AddKey (pos, 4f*(-(1-pos)*(1-pos)* (((s * 1.525f)+1.8f) * (1-pos) - s )) +1);
				}

			
			}

			return curve;

		}


		public static AnimationCurve EaseInElastic(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;
			
			float p = 0.3f;
			float a = 1f;
			float s = p / (2f * Mathf.PI) * Mathf.Asin(1f / a);


			for (int i = 0; i <= maxKey; i++) {
				float pos = i /maxKey;
			

				curve.AddKey (pos,	-(a * Mathf.Pow(2, 10f * (pos -= 1)) * Mathf.Sin((pos * 1f - s) * (2f * Mathf.PI) / p)));
			}

			return curve;

		}
		public static  AnimationCurve EaseOutElastic(){

			AnimationCurve curve = new AnimationCurve();
			int maxKey = 30;
			
			float p = 0.3f;
			float a = 1f;
			float s = p / (2f * Mathf.PI) * Mathf.Asin(1f / a);


			for (int i = 0; i <= maxKey; i++) {
				float pos = i / maxKey;


				curve.AddKey (1-pos, -(a * -Mathf.Pow(2, 10f * (pos -= 1)) * Mathf.Sin((pos * 1f - s) * (2f * Mathf.PI) / p))+1 );
			}

			return curve;


		}
	}
}
