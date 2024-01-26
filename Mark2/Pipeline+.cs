using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using static Mark2.Pipeline;
//  
//  88888888ba                88           88  88                             
//  88      "8b               88           88  ""                             
//  88      ,8P               88           88                                 
//  88aaaaaa8P'  88       88  88,dPPYba,   88  88   ,adPPYba,                 
//  88""""""'    88       88  88P'    "8a  88  88  a8"     ""                 
//  88           88       88  88       d8  88  88  8b                         
//  88           "8a,   ,a88  88b,   ,a8"  88  88  "8a,   ,aa                 
//  88            `"YbbdP'Y8  8Y"Ybbd8"'   88  88   `"Ybbd8"'                 
//                                                                            
//                                                                            
//                                                                            
//  88888888ba   88                           88  88                          
//  88      "8b  ""                           88  ""                          
//  88      ,8P                               88                              
//  88aaaaaa8P'  88  8b,dPPYba,    ,adPPYba,  88  88  8b,dPPYba,    ,adPPYba, 
//  88""""""'    88  88P'    "8a  a8P_____88  88  88  88P'   `"8a  a8P_____88 
//  88           88  88       d8  8PP"""""""  88  88  88       88  8PP""""""" 
//  88           88  88b,   , a8" "8b,   ,aa  88  88  88       88  "8b,   ,aa 
//  88           88  88`YbbdP"'    `"Ybbd8"'  88  88  88       88   `"Ybbd8"' 
//                   88                                                       
//                   88                                                       

namespace Mark2 {
    public partial class Pipeline {
        public Pipeline() : this(64) { }
        public Pipeline(int maxChunkSize) {
            _maxChunkSize = maxChunkSize;
        }
        public void Chanel<ConduitT>(Action<ConduitT> Initializer, Action<ConduitT> Finalizer) where ConduitT : IConduit<ConduitT>, new(){
            Type conduitType = typeof(ConduitT);
            ChannelAbstract channel;
            if (_lastChannelConduitType == conduitType) {
                channel = _lastChannel;
            } else {
                if (!_conduitTypeToChannelMap.TryGetValue(conduitType, out channel)) {
                    _conduitTypeToChannelMap[conduitType] = channel = new ChannelClass<ConduitT>(this);
                }
            }
            _lastChannelConduitType = conduitType;
            _lastChannel = channel;
            (channel as ChannelClass<ConduitT>).AddConduit(Initializer, Finalizer);
        }
        public void Flush() {
            foreach(var channel in _conduitTypeToChannelMap.Values) {
                channel.Flush();
            }
        }
    }
}
