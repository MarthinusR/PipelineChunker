using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
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
        public Pipeline(int maxChunkSize = 64) {
            _maxChunkSize = maxChunkSize;
        }

        public void Chanel<ConduitT>(Action<ConduitT> Initializer, Action<ConduitT> Finalizer) {

        }
        public void Flush() {

        }
    }
}
