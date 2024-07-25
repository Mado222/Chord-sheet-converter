﻿namespace FeedbackDataLib.Modules
{
    [Serializable()]    //Set this attribute to all the classes that want to serialize
    public class CModuleEEG : CModuleExGADS1292_EEG
    {
        public CModuleEEG()
        {
            _num_raw_Channels = 1;
            _ModuleType_Unmodified = enumModuleType.cModuleEEG;
            _ModuleType = enumModuleType.cModuleEEG;
            Init();
        }

        public override byte[] Get_SWConfigChannelsByteArray()
        {
            if (SWChannels != null)
            {
                SWChannels_Module[0].SWConfigChannel.SampleInt = SWChannels[0].SWConfigChannel.SampleInt;
                return base.Get_SWConfigChannelsByteArray();
            }
            return [];
        }
    }
}
