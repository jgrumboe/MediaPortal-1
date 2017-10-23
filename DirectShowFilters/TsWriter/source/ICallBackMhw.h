/*
 *  Copyright (C) 2006-2008 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
#pragma once
#include <map>
#include <vector>

using namespace std;


class ICallBackMhw
{
  public:
    virtual ~ICallBackMhw() {}

    virtual void OnMhwEventRemoved(unsigned char version,
                                    unsigned char segment,
                                    unsigned long eventId,
                                    unsigned long descriptionId,
                                    bool hasDescription,
                                    unsigned char channelId,
                                    unsigned long long startDateTime,
                                    unsigned short duration,
                                    const char* title,
                                    bool isPayPerView,
                                    unsigned long payPerViewId,
                                    bool isTerrestrial,
                                    unsigned long programId,
                                    unsigned long showingId,
                                    bool isHighDefinition,
                                    bool hasSubtitles,
                                    unsigned char themeId,
                                    unsigned char subThemeId) = 0;
};