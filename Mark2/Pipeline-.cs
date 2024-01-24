using System;
using System.Collections.Generic;
using System.Text;
//  
//  88888888ba               88                                              
//  88      "8b              ""                             ,d               
//  88      ,8P                                             88               
//  88aaaaaa8P'  8b,dPPYba,  88  8b       d8  ,adPPYYba,  MM88MMM  ,adPPYba, 
//  88""""""'    88P'   "Y8  88  `8b     d8'  ""     `Y8    88    a8P_____88 
//  88           88          88   `8b   d8'   ,adPPPPP88    88    8PP""""""" 
//  88           88          88    `8b,d8'    88,    ,88    88,   "8b,   ,aa 
//  88           88          88      "8"      `"8bbdP"Y8    "Y888  `"Ybbd8"' 
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
// 
namespace Mark2 {
    public partial class Pipeline {
        Type _lastChannelConduitType;
        ChannelAbstract _lastChannel;
        private readonly int _maxChunkSize;
        private readonly Dictionary<Type, ChannelAbstract> _conduitTypeToChannelMap = new Dictionary<Type, ChannelAbstract>();
    }
}
