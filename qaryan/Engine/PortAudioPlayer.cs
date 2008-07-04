/* This file is part of Qaryan.
 * 
 * BASED ON Audio class from PortAudioSharp - PortAudio bindings for .NET
 * Copyright 2006-2007, Riccardo Gerosa, and individual contributors as indicated
 * by the @authors tag. See the copyright.txt in the distribution for a
 * full listing of individual contributors.
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org
 */

using System;
using System.Runtime.InteropServices;
using PortAudioSharp;

namespace Qaryan.Audio
{

    /**
        <summary>
            A simplified high-level audio class
        </summary>
    */
    public class PortAudioPlayer : IDisposable
    {

        private int channels, frequency;
        private uint framesPerBuffer;
        private PortAudio.PaStreamCallbackDelegate paStreamCallback;
        private int hostApi;
        PortAudio.PaHostApiInfo apiInfo;
        PortAudio.PaDeviceInfo inputDeviceInfo, outputDeviceInfo;
        private IntPtr stream;
        private static bool loggingEnabled = false;
        private bool disposed = false;

        public static bool LoggingEnabled
        {
            get { return loggingEnabled; }
            set { loggingEnabled = value; }
        }

        public PortAudioPlayer(int channels, int frequency, uint framesPerBuffer,
            PortAudio.PaStreamCallbackDelegate paStreamCallback)
        {
            log("Initializing...");
            this.channels = channels;
            this.frequency = frequency;
            this.framesPerBuffer = framesPerBuffer;
            this.paStreamCallback = paStreamCallback;
            if (errorCheck("Initialize", PortAudio.Pa_Initialize()))
            {
                this.disposed = true;
                // if Pa_Initialize() returns an error code, 
                // Pa_Terminate() should NOT be called.
                throw new Exception("Can't initialize audio");
            }
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i < apiCount; i++)
            {
                PortAudio.PaHostApiInfo availableApiInfo = PortAudio.Pa_GetHostApiInfo(i);
                log("available API index: " + i + "\n" + availableApiInfo.ToString());
            }
            this.hostApi = apiSelect();
            log("selected Host API: " + this.hostApi);
            this.apiInfo = PortAudio.Pa_GetHostApiInfo(this.hostApi);
            this.inputDeviceInfo = PortAudio.Pa_GetDeviceInfo(apiInfo.defaultInputDevice);
            this.outputDeviceInfo = PortAudio.Pa_GetDeviceInfo(apiInfo.defaultOutputDevice);
            log("input device:\n" + inputDeviceInfo.ToString());
            log("output device:\n" + outputDeviceInfo.ToString());
        }

        public void Start()
        {
            log("Starting...");
            this.stream = streamOpen(this.apiInfo.defaultInputDevice, this.channels,
                this.apiInfo.defaultOutputDevice, this.channels,
                this.frequency, this.framesPerBuffer);
            //this.stream = audio.streamOpen(this.channels,this.channels,
            //	this.frequency,this.framesPerBuffer);
            log("Stream pointer: " + stream.ToInt32());
            streamStart(stream);
        }

        public void Stop()
        {
            log("Stopping...");
            streamStop(this.stream);
            streamClose(this.stream);
            this.stream = new IntPtr(0);
        }

        private void log(String logString)
        {
            if (loggingEnabled)
                System.Console.WriteLine("PortAudio: " + logString);
            System.Console.WriteLine();
        }

        private bool errorCheck(String action, PortAudio.PaErrorCode errorCode)
        {
            if (errorCode != PortAudio.PaErrorCode.paNoError)
            {
                log(action + " error: " + PortAudio.Pa_GetErrorText(errorCode));
                if (errorCode == PortAudio.PaErrorCode.paUnanticipatedHostError)
                {
                    PortAudio.PaHostErrorInfo errorInfo = PortAudio.Pa_GetLastHostErrorInfo();
                    log("- Host error API type: " + errorInfo.hostApiType);
                    log("- Host error code: " + errorInfo.errorCode);
                    log("- Host error text: " + errorInfo.errorText);
                }
                return true;
            }
            else
            {
                log(action + " OK");
                return false;
            }
        }

        private int apiSelect()
        {
            int selectedHostApi = PortAudio.Pa_GetDefaultHostApi();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i < apiCount; i++)
            {
                PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(i);
                if ((apiInfo.type == PortAudio.PaHostApiTypeId.paDirectSound)
                    || (apiInfo.type == PortAudio.PaHostApiTypeId.paOSS))
                    selectedHostApi = i;
            }
            return selectedHostApi;
        }

        private IntPtr streamOpen(int inputDevice, int inputChannels,
                         int outputDevice, int outputChannels,
                         int sampleRate, uint framesPerBuffer)
        {
            IntPtr stream = new IntPtr();
            IntPtr data = new IntPtr(0);
            PortAudio.PaStreamParameters inputParams = new PortAudio.PaStreamParameters();
            inputParams.channelCount = inputChannels;
            inputParams.device = inputDevice;
            inputParams.sampleFormat = PortAudio.PaSampleFormat.paInt16;
            inputParams.suggestedLatency = 0.0;
            PortAudio.PaStreamParameters outputParams = new PortAudio.PaStreamParameters();
            outputParams.channelCount = outputChannels;
            outputParams.device = outputDevice;
            outputParams.sampleFormat = PortAudio.PaSampleFormat.paInt16;
            outputParams.suggestedLatency = 0.0;
            errorCheck("OpenDefaultStream", PortAudio.Pa_OpenStream(
                out stream,
                ref inputParams,
                ref outputParams,
                sampleRate,
                framesPerBuffer,
                PortAudio.PaStreamFlags.paNoFlag,
                this.paStreamCallback,
                data));
            return stream;
        }

        /*
        private IntPtr streamOpen(int inputChannels,int outputChannels,
            int sampleRate, uint framesPerBuffer) {
            IntPtr stream = new IntPtr();
            IntPtr data = new IntPtr(0);
            errorCheck("OpenDefaultStream",PortAudio.Pa_OpenDefaultStream(
                out stream,
                inputChannels,
                outputChannels,
                (uint) PortAudio.PaSampleFormat.paFloat32,
                sampleRate,
                framesPerBuffer,
                this.paStreamCallback,
                data));     
            return stream;
        }
        */

        private void streamClose(IntPtr stream)
        {
            errorCheck("CloseStream", PortAudio.Pa_CloseStream(stream));
        }

        private void streamStart(IntPtr stream)
        {
            errorCheck("StartStream", PortAudio.Pa_StartStream(stream));
        }

        private void streamStop(IntPtr stream)
        {
            errorCheck("StopStream", PortAudio.Pa_StopStream(stream));
        }

        /*
        private void streamWrite(IntPtr stream, float[] buffer) {
            errorCheck("WriteStream",PortAudio.Pa_WriteStream(
                stream,buffer,(uint)(buffer.Length/2)));
        }
        */

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // Dispose here any managed resources
                }

                // Dispose here any unmanaged resources
                log("Terminating...");
                errorCheck("Terminate", PortAudio.Pa_Terminate());
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PortAudioPlayer()
        {
            Dispose(false);
        }
    }

}
