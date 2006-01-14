/* $Id: HBMp3Encoder.cpp,v 1.6 2003/08/25 20:41:51 titer Exp $ */

#include "HBCommon.h"
#include "HBMp3Encoder.h"
#include "HBManager.h"
#include "HBFifo.h"

#include <lame/lame.h>

HBMp3Encoder::HBMp3Encoder( HBManager * manager, HBAudioInfo * audioInfo )
    : HBThread( "mp3encoder" )
{
    fManager      = manager;
    fAudioInfo    = audioInfo;
    
    fRawBuffer    = NULL;
    fPosInBuffer  = 0;
    fLeftSamples  = NULL;
    fRightSamples = NULL;
}

void HBMp3Encoder::DoWork()
{
    /* Wait a first buffer so we are sure that
       fAudioInfo->fInSampleRate (set the AC3 decoder) is not garbage */
    while( !fDie && !fAudioInfo->fRawFifo->Size() )
    {
        snooze( 5000 );
    }
    
    /* The idea is to have exactly one mp3 frame (i.e. 1152 samples) by
       output buffer. As we are resampling from fInSampleRate to
       fOutSampleRate, we will give ( 1152 * fInSampleRate ) /
       ( 2 * fOutSampleRate ) to libmp3lame so we are sure we will
       never get more than 1 frame at a time */
    uint32_t count = ( 1152 * fAudioInfo->fInSampleRate ) /
                         ( 2 * fAudioInfo->fOutSampleRate );

    /* Init libmp3lame */
    lame_global_flags * globalFlags = lame_init();
    lame_set_in_samplerate( globalFlags, fAudioInfo->fInSampleRate );
    lame_set_out_samplerate( globalFlags, fAudioInfo->fOutSampleRate );
    lame_set_brate( globalFlags, fAudioInfo->fOutBitrate );
    
    if( lame_init_params( globalFlags ) == -1 )
    {
        Log( "HBMp3Encoder::DoWork() : lame_init_params() failed" );
        fManager->Error();
        return;
    }

    fLeftSamples  = (float*) malloc( count * sizeof( float ) );
    fRightSamples = (float*) malloc( count * sizeof( float ) );

    HBBuffer * mp3Buffer = new HBBuffer( LAME_MAXMP3BUFFER );
    int        ret;
    while( !fDie )
    {
        /* Get new samples */
        if( !GetSamples( count ) )
        {
            continue;
        }

        ret = lame_encode_buffer_float( globalFlags,
                                        fLeftSamples, fRightSamples,
                                        count, mp3Buffer->fData,
                                        mp3Buffer->fSize );
        
        if( ret < 0 )
        {
            /* Something wrong happened */
            Log( "HBMp3Encoder : lame_encode_buffer_float() failed (%d)", ret );
            fManager->Error();
            break;
        }
        else if( ret > 0 )
        {
            /* We got something, send it to the muxer */
            mp3Buffer->fSize = ret;
            mp3Buffer->fKeyFrame = true;
            fAudioInfo->fMp3Fifo->Push( mp3Buffer );
            mp3Buffer = new HBBuffer( LAME_MAXMP3BUFFER );
        }
    }
    
    /* Clean up */
    delete mp3Buffer;
    free( fLeftSamples );
    free( fRightSamples );
    
    lame_close( globalFlags );
}

bool HBMp3Encoder::GetSamples( uint32_t count )
{
    uint32_t samplesNb = 0;

    while( samplesNb < count )
    {
        if( !fRawBuffer )
        {
            fRawBuffer = fAudioInfo->fRawFifo->Pop();
            
            if( !fRawBuffer )
            {
                return false;
            }
            
            fPosInBuffer = 0;
        }
        
        int willCopy = MIN( count - samplesNb, 6 * 256 - fPosInBuffer );
        
        memcpy( fLeftSamples + samplesNb,
                (float*) fRawBuffer->fData + fPosInBuffer,
                willCopy * sizeof( float ) );
        memcpy( fRightSamples + samplesNb,
                (float*) fRawBuffer->fData + 6 * 256 + fPosInBuffer,
                willCopy * sizeof( float ) );
        
        samplesNb    += willCopy;   
        fPosInBuffer += willCopy;
        
        if( fPosInBuffer == 6 * 256 )
        {
            delete fRawBuffer;
            fRawBuffer = NULL;
        }
    }
    
    return true;
}
