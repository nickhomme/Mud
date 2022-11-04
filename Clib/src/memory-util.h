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
#endif //MUD_MEMORY_UTIL_H_
