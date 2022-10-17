//
// Created by Nicholas Homme on 9/30/19.
//

#ifndef MUD_MEMORY_UTIL_H_
#define MUD_MEMORY_UTIL_H_

#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#ifndef _WIN32
#include <printf.h>
#endif

typedef void* ptr;
typedef const void* ptr_const;
#define null NULL
#define safe_free(p) safer_free((void**)&(p))

static void safer_free(void **pp) {
  if (pp != null && *pp != null) {

//    printf("Freeing: [%s]\n", *pp);
    free(*pp);
    *pp = null;
  }
}

#ifdef _WIN32
#  define EXPORTIT __declspec( dllexport )
#else
#  define EXPORTIT __attribute__((visibility("default")))
#endif


extern ptr _store_string(char *val);
extern char* _store_read_string(ptr pointer);

extern ptr _store_char(char val);
extern char _store_read_char(ptr pointer);

extern ptr _store_bool(bool val);
extern bool _store_read_bool(ptr pointer);

extern ptr _store_float(float val);
extern float _store_read_float(ptr pointer);

extern ptr _store_int(int val);
extern int _store_read_int(ptr pointer);

extern void _store_free(ptr pointer);

#endif //MUD_MEMORY_UTIL_H_
