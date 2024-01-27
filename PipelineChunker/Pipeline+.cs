using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using static PipelineChunker.Pipeline;
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

namespace PipelineChunker {
    public partial class Pipeline {
#if DEBUG
        static int cacheHit = 0;
        static int cacheMiss = 0;
#endif
        public Pipeline() : this(64) { }
        public Pipeline(int maxChunkSize) {
            _maxChunkSize = maxChunkSize;
        }
        public void Chanel<ConduitT>(Action<ConduitT> Initializer, Action<ConduitT, Exception, ExceptionCommunicator> Finalizer) where ConduitT : Conduit<ConduitT>, new(){
            Type conduitType = typeof(ConduitT);
            ChannelAbstract channel;
            // use one level of cashing to avoid potential dictionary lookup
            if (_lastChannelConduitType == conduitType) {
#if DEBUG
                cacheHit++;
#endif
                channel = _lastChannel;
            } else {
#if DEBUG
                cacheMiss++;
#endif
                if (!_conduitTypeToChannelMap.TryGetValue(conduitType, out channel)) {
                    _conduitTypeToChannelMap[conduitType] = channel = new ChannelClass<ConduitT>(this);
                }
            }
#if DEBUG
            Debug.WriteLine($"Pipeline.Chanel<{conduitType.FullName}> cache [hit: {cacheHit}, miss: {cacheMiss}]");
#endif
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
