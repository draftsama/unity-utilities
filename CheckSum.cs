using System;
using System.Text;

namespace Modules.Utilities
{
	public class CheckSum
	{
		public static string Generate(string _text)
		{
			string result;
			
			using (var md5 = System.Security.Cryptography.MD5.Create()) 
			{
				result	=	BitConverter.ToString
							(
								md5.ComputeHash
								(
									Encoding.UTF8.GetBytes
									(
										_text
									)
								)
							).Replace("-", string.Empty);
			}
			
			return result;
		}
	}
}