using System;
using System.Collections;
using System.Windows.Forms;
using DShowNET;
using MediaPortal.TV.Database;
using MediaPortal.TV.Recording;
using MediaPortal.GUI.Library;
using System.Xml;

namespace MediaPortal.TV.Recording
{

	/// <summary>
	/// Summary description for DVBTTuning.
	/// </summary>
	public class DVBTTuning : ITuning
	{
		enum State
		{
			ScanFrequencies,
			ScanChannels,
			ScanOffset
		}
		TVCaptureDevice											captureCard;
		AutoTuneCallback										callback = null;
		ArrayList                           frequencies=new ArrayList();
		int                                 currentFrequencyIndex=0;
		int																	scanOffset = 0;
		private System.Windows.Forms.Timer  timer1;
		State                               currentState;
		DateTime														channelScanTimeOut;

		public DVBTTuning()
		{
		}
		#region ITuning Members

		public void AutoTuneTV(TVCaptureDevice card, AutoTuneCallback statusCallback)
		{
			captureCard=card;
			callback=statusCallback;

			currentState=State.ScanFrequencies;
			frequencies.Clear();
			currentFrequencyIndex=0;
			String countryCode = String.Empty;

			Log.Write("Opening dvbt.xml");
			XmlDocument doc= new XmlDocument();
			doc.Load("dvbt.xml");

			FormCountry formCountry = new FormCountry();
			XmlNodeList countryList=doc.DocumentElement.SelectNodes("/dvbt/country");
			foreach (XmlNode nodeCountry in countryList)
			{
				string name= nodeCountry.Attributes.GetNamedItem(@"name").InnerText;
				formCountry.AddCountry(name);
			}
			formCountry.ShowDialog();
			string countryName=formCountry.countryName;
			if (countryName==String.Empty) return;
			Log.Write("auto tune for {0}", countryName);
			frequencies.Clear();

			countryList=doc.DocumentElement.SelectNodes("/dvbt/country");
			foreach (XmlNode nodeCountry in countryList)
			{
				string name= nodeCountry.Attributes.GetNamedItem(@"name").InnerText;
				if (name!=countryName) continue;
				Log.Write("found country {0} in dvbt.xml", countryName);
				try
				{
					scanOffset =  XmlConvert.ToInt32(nodeCountry.Attributes.GetNamedItem(@"offset").InnerText);
					Log.Write("scanoffset: {0} ", scanOffset);
				}
				catch(Exception){}

				XmlNodeList frequencyList = nodeCountry.SelectNodes("carrier");
				Log.Write("number of carriers:{0}", frequencyList.Count);
				int[] carrier;
				foreach (XmlNode node in frequencyList)
				{
					carrier = new int[2];
					carrier[0] = XmlConvert.ToInt32(node.Attributes.GetNamedItem(@"frequency").InnerText);
					try
					{
						carrier[1] = XmlConvert.ToInt32(node.Attributes.GetNamedItem(@"bandwidth").InnerText);
					}
					catch(Exception){}

					frequencies.Add(carrier);
					Log.Write("added:{0}", carrier[0]);
				}
			}
			if (frequencies.Count==0) return;

			Log.Write("loaded:{0} frequencies", frequencies.Count);
			Log.Write("{0} has a scan offset of {1}KHz", countryCode, scanOffset);
			this.timer1 = new System.Windows.Forms.Timer();
			this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
			timer1.Interval=100;
			timer1.Enabled=true;
			callback.OnProgress(0);
			return;
		}

		public void AutoTuneRadio(TVCaptureDevice card, AutoTuneCallback callback)
		{
			// TODO:  Add DVBTTuning.AutoTuneRadio implementation
		}

		public void Continue()
		{
			// TODO:  Add DVBTTuning.Continue implementation
		}

		public int MapToChannel(string channel)
		{
			// TODO:  Add DVBTTuning.MapToChannel implementation
			return 0;
		}

		private void timer1_Tick(object sender, System.EventArgs e)
		{
			if (currentFrequencyIndex > frequencies.Count)
				return;
			
			float percent = ((float)currentFrequencyIndex) / ((float)frequencies.Count);
			percent *= 100.0f;
			callback.OnProgress((int)percent);
			int[] tmp = frequencies[currentFrequencyIndex] as int[];
			//Log.Write("FREQ: {0} BWDTH: {1}", tmp[0], tmp[1]);
			float frequency = (float)tmp[0];
			frequency /=1000;
			string description=String.Format("frequency:{0:###.##} MHz.", frequency);
			callback.OnStatus(description);

			if (currentState==State.ScanFrequencies)
			{
				if (captureCard.SignalPresent())
				{
					Log.Write("Found signal at:{0} MHz",frequency);
					currentState=State.ScanChannels;
					channelScanTimeOut=DateTime.Now;
				} 
				else 
				{
					int[] scanObject;
					for (int i = 0; i < 2; i++)
					{
            scanObject = frequencies[currentFrequencyIndex] as int[];
						if (i == 0)
						{
							scanObject[0] -= scanOffset;
							//Log.Write("trying offset -{0} of {1)", scanOffset, scanObject[0]);
							description=String.Format("frequency:{0:###.##} MHz. - trying offset -{1}", frequency, scanOffset);
							
						}
						else if (i == 1)
						{
							scanObject[0] += scanOffset;
							//Log.Write("trying offset +{0} of {1)", scanOffset, scanObject[0]);
							description=String.Format("frequency:{0:###.##} MHz. - trying offset +{1}", frequency, scanOffset);
						}
						captureCard.Tune(scanObject);
						callback.OnStatus(description);
						if (captureCard.SignalPresent())
						{
							Log.Write("Found signal at:{0} MHz", scanObject[0] / 1000);
							currentState=State.ScanChannels;
							channelScanTimeOut=DateTime.Now;
						} 
					} 
				}
			}

			if (currentState==State.ScanFrequencies)
			{
				callback.OnStatus(description);
				ScanNextFrequency();
			}

			if (currentState==State.ScanChannels)
			{
				description=String.Format("Found signal at frequency:{0:###.##} MHz. Scanning channels", frequency);
				callback.OnStatus(description);
				ScanChannels();
			}
			
		}

		void ScanChannels()
		{
			captureCard.Process();

			TimeSpan ts = DateTime.Now-channelScanTimeOut;
			if (ts.TotalSeconds>=15)
			{
				captureCard.StoreTunedChannels(false,true);
				callback.UpdateList();
				Log.Write("timeout, goto scanning frequencies");
				currentState=State.ScanFrequencies;
				ScanNextFrequency();
				return;
			}
		}

		void ScanNextFrequency()
		{
			currentFrequencyIndex++;
			if (currentFrequencyIndex>=frequencies.Count)
			{
				timer1.Enabled=false;
				callback.OnProgress(100);
				callback.OnEnded();
				captureCard.DeleteGraph();
				return;
			}

			Log.Write("tune:{0}",(frequencies[currentFrequencyIndex] as int[])[0]);
			captureCard.Tune(frequencies[currentFrequencyIndex]);
		}

		#endregion
	}
}
