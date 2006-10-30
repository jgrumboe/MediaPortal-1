using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TvLibrary;
using TvDatabase;
using TvLibrary.Log;
using TvLibrary.Interfaces;
using TvLibrary.Implementations;
using TvLibrary.Channels;
using TvControl;

namespace SetupTv.Sections
{
  public partial class FormPreview : Form
  {
    Channel _channel;
    VirtualCard _card = null;
    Player _player = null;
    public FormPreview()
    {
      InitializeComponent();
    }

    public Channel Channel
    {
      get
      {
        return _channel;
      }
      set
      {
        _channel = value;
      }
    }

    private void FormPreview_Load(object sender, EventArgs e)
    {
      this.Text = "Preview " + _channel.Name;

      TvServer server = new TvServer();
      TvResult result;
      result = server.StartTimeShifting(_channel.Name, out _card);
      if (result != TvResult.Succeeded)
      {
        MessageBox.Show("Preview failed:" + result.ToString());
        return;
      }
      _player = new Player();
      _player.Play(_card.TimeShiftFileName, this);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
      if (_player != null)
      {
        _player.Stop();
        _player = null;
      }
      if (_card != null)
      {
        _card.StopTimeShifting();
      }
      base.OnClosing(e);
    }
  }
}