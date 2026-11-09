#include <windows.h>
extern "C" __declspec(dllexport) volatile unsigned short h1_test_slot[4]={7,123,9,456};
extern "C" __declspec(dllexport) __declspec(noinline) int h1_probe(){unsigned a=h1_test_slot[1];h1_test_slot[1]=987;unsigned b=h1_test_slot[1];unsigned c=h1_test_slot[3];return a==123&&b==987&&c==456?0:7;}
DWORD WINAPI writer(void*){h1_test_slot[1]=1234;return 0;}
int main(){if(h1_probe())return 7;HANDLE thread=CreateThread(0,0,writer,0,0,0);if(!thread)return 8;WaitForSingleObject(thread,INFINITE);CloseHandle(thread);unsigned d=h1_test_slot[1];unsigned wide=((volatile unsigned*)h1_test_slot)[0];return d==1234&&(wide>>16)==1234?0:9;}
