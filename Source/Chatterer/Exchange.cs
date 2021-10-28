///////////////////////////////////////////////////////////////////////////////
//
//    Chatterer /L Unleashed, a plugin for Kerbal Space Program from SQUAD
//    (https://www.kerbalspaceprogram.com/)
//    Copyright (C) 2020-21 LisiasT 
//    Copyright (C) 2014-20 Athlonic 
//    Copyright (C) 2013 Iannic-ann-od
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
///////////////////////////////////////////////////////////////////////////////

using System.Collections;
using UnityEngine;

namespace Chatterer
{
    public partial class chatterer : MonoBehaviour
    {
        IEnumerator Exchange(float delay)
        {
            Log.dbg("Beginning Cor_exchange");

            exchange_playing = true;
            initialize_new_exchange();

            if (ChatStarter.pod == initial_chatter_source)
            {
                //capsule starts the exchange
                //always play regardless of radio contact state
                Log.dbg("Capsule starts the exchange...");

                //play initial capsule chatter
                if (initial_chatter_set.Count > 0)
                {
                    if (initial_chatter.isPlaying == false)
                    {
                        initial_chatter.Play();
                        Log.dbg("playing initial chatter");
                        yield return new WaitForSeconds(initial_chatter.clip.length);
                        //initial chatter has finished playing
                    }
                    else Log.warn("initial_chatter already playing, move on...");
                }
                else
                {
                    exchange_playing = false;
                    Log.warn("initial_chatter_set has no audioclips, abandoning exchange");
                }
            }
            else if (ChatStarter.capcom == initial_chatter_source)
            {
                //capcom starts the exchange
                Log.dbg("Capcom starts the exchange...");

                if (inRadioContact)
                {
                    //in radio contact,
                    //play initial capcom

                    if (initial_chatter_set.Count > 0)
                    {
                        if (quindar_toggle)
						{
							initial_chatter.PlayOneShot(quindar_intro.clip); // Workaround [playing quindar with quindar.audiosource is BROKEN : hangs exchange since KSP v1.4.0 for some weird reasons]
							Log.dbg("playingOneShot initial quindar");
							yield return new WaitForSeconds(quindar_intro.clip.length);

							initial_chatter.Play();
							Log.dbg("playing initial chatter");
							yield return new WaitForSeconds(initial_chatter.clip.length);

							initial_chatter.PlayOneShot(quindar_outro.clip); // Workaround [playing quindar with quindar.audiosource is BROKEN : hangs exchange since KSP v1.4.0 for some weird reasons]
							Log.dbg("playingOneShot outro quindar");
							yield return new WaitForSeconds(quindar_outro.clip.length);

							//initial chatter has finished playing
						}
						else                  // play without quindar
						{
                            initial_chatter.Play();
                            Log.dbg("playing initial chatter");
                            yield return new WaitForSeconds(initial_chatter.clip.length);
                            //initial chatter has finished playing
                        }
                    }
                    else
                    {
                        exchange_playing = false;
                        Log.warn("initial_chatter_set has no audioclips, abandoning exchange");
                    }
                }
                else
                {
                    //not in radio contact,
                    //play no initial chatter or response
                    Log.dbg("No radio contact, Capcom is speaking with void.");
                    exchange_playing = false;
                }
            }
            
            //so respond now
            Log.dbg("Responding Cor_exchange, delay = {0}", response_delay_secs);

            yield return new WaitForSeconds(response_delay_secs);

            if (response_chatter_set.Count > 0 && (inRadioContact))
            {
                Log.dbg("playing response");
                if (ChatStarter.pod == initial_chatter_source)
                {
                    Log.dbg("Capcom responding");

                    if (quindar_toggle)
                    {
                        response_chatter.Play();
                        Log.dbg("playing response chatter");
                        yield return new WaitForSeconds(response_chatter.clip.length);

                        response_chatter.PlayOneShot(quindar_outro.clip); // Workaround [playing quindar with quindar.audiosource is BROKEN : hangs exchange since KSP v1.4.0 for some weird reasons]
                        Log.dbg("playingOneShot response quindar");
                        yield return new WaitForSeconds(quindar_outro.clip.length);

                        //response chatter has finished playing

                    }
                    else                  // play without quindar
                    {
                        response_chatter.Play();
                        Log.dbg("playing response chatter");
                        yield return new WaitForSeconds(response_chatter.clip.length);
                        //response chatter has finished playing
                    }
                }
                else if (ChatStarter.capcom == initial_chatter_source)
                {
                    if (response_chatter.isPlaying == false)
                    {
                        Log.dbg("Capsule responding");

                        response_chatter.PlayDelayed(delay);

                        yield return new WaitForSeconds(response_chatter.clip.length);
                        //response chatter has finished playing
                    }
                    else Log.warn("response_chatter already playing, move on...");
                }
            }
            else if (response_chatter_set.Count > 0 && !inRadioContact)
            {
                if (exchange_playing == true)
                {
                    Log.info("No connection, no response ... you are alone !");
                    exchange_playing = false;
                }
            }
            else
            {
                Log.warn("response_chatter_set has no audioclips, abandoning exchange");
                exchange_playing = false;   //exchange is over
            }

            exchange_playing = false;
            Log.dbg("exchange is over");
        }
    }
}
