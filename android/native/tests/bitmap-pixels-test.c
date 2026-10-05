#include <assert.h>
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
int nh_rgba_to_bgra(const uint8_t*, size_t, uint32_t, uint32_t, uint32_t, uint8_t*, size_t);
int main(void) {
    uint8_t src[] = {30,20,10,128,99,99,99,99,255,4,90,255,99,99,99,99};
    uint8_t dst[8] = {0};
    assert(nh_rgba_to_bgra(src,16,1,2,8,dst,8)==0);
    assert(dst[0]==5 && dst[1]==10 && dst[2]==15 && dst[3]==128);
    assert(dst[4]==90 && dst[5]==4 && dst[6]==255 && dst[7]==255);
    for (unsigned a=0;a<256;a++) for (unsigned c=0;c<256;c++) {
        src[0]=src[1]=src[2]=c; src[3]=a;
        assert(nh_rgba_to_bgra(src,4,1,1,4,dst,4)==0);
        assert(dst[0]==(uint8_t)(c*(a/255.f)+.5f) && dst[3]==a);
    }
    dst[0]=42;
    assert(nh_rgba_to_bgra(src,4,UINT32_MAX,1,4,dst,4)==-1);
    assert(nh_rgba_to_bgra(src,4,0,1,4,dst,4)==-1);
    assert(nh_rgba_to_bgra(src,3,1,1,4,dst,4)==-1);
    assert(nh_rgba_to_bgra(src,4,1,1,3,dst,4)==-1);
    assert(nh_rgba_to_bgra(src,4,1,1,4,dst,3)==-1);
    assert(nh_rgba_to_bgra(NULL,4,1,1,4,dst,4)==-1);
    assert(dst[0]==42);
    puts("PASS: all 65536 alpha/channel combinations, padding, bounds");
}
